#!/usr/bin/env python3
"""Moves milestone tasks from `blocked` to `ready` once nothing holds them back.

Reads two fields from the first line of each task issue, as the brief template in
docs/parallel-development.md writes them:

    Milestone plan: docs/plans/m1.md · Depends on: M1-02, M1-03 · Gate: — · Size: M

A task moves to `ready` when:
- every task named in "Depends on" is closed as completed (a task closed as
  "not planned" does not count: the owner re-plans the dependants by hand), or
  "Depends on" is "all" and every other task of the milestone is closed;
- "Gate" is "—", so no owner decision is pending. A gated task waits until the
  owner edits its gate to "—" or moves it by hand.

An issue without both fields is left alone. Needs the gh CLI with GH_TOKEN and GH_REPO set.
Set DRY_RUN=1 to print the changes without making them.
"""
import json
import os
import re
import subprocess
import sys

TASK_ID = re.compile(r"\bM\d+-[0-9A-Z]+\b")
TITLE_ID = re.compile(r"^(M\d+-[0-9A-Z]+):")
MILESTONE_LABEL = re.compile(r"^m\d+$")
NO_GATE = {"—", "–", "-", ""}
DRY_RUN = os.environ.get("DRY_RUN") == "1"


def gh(*args: str) -> str:
    return subprocess.run(["gh", *args], check=True, capture_output=True, text=True).stdout


def field(body: str, name: str) -> str | None:
    match = re.search(rf"{re.escape(name)}:[ \t]*([^·\n]*)", body)
    return match.group(1).strip() if match else None


def is_done(issue: dict) -> bool:
    return issue["state"] == "CLOSED" and issue.get("stateReason") != "NOT_PLANNED"


def main() -> int:
    issues = json.loads(gh("issue", "list", "--state", "all", "--limit", "1000",
                           "--json", "number,title,state,stateReason,labels,body"))
    by_task = {}
    for issue in issues:
        match = TITLE_ID.match(issue["title"])
        if match:
            by_task[match.group(1)] = issue

    for task, issue in sorted(by_task.items(), key=lambda kv: kv[1]["number"]):
        labels = {label["name"] for label in issue["labels"]}
        if issue["state"] != "OPEN" or "blocked" not in labels:
            continue
        body = issue["body"] or ""
        depends, gate = field(body, "Depends on"), field(body, "Gate")
        if depends is None or gate is None:
            print(f"#{issue['number']} {task}: no 'Depends on'/'Gate' fields, left for the coordinator")
            continue
        if gate not in NO_GATE:
            print(f"#{issue['number']} {task}: waits for owner decision {gate}")
            continue

        if depends.lower().startswith("all"):
            milestones = {label for label in labels if MILESTONE_LABEL.match(label)}
            others = sorted((other for other_task, other in by_task.items() if other_task != task
                             and milestones & {label["name"] for label in other["labels"]}),
                            key=lambda other: other["number"])
            waiting = [TITLE_ID.match(o["title"]).group(1) for o in others if o["state"] != "CLOSED"]
        else:
            needed = [d for d in TASK_ID.findall(depends) if d != task]
            unknown = [d for d in needed if d not in by_task]
            if unknown:
                print(f"#{issue['number']} {task}: unknown dependencies {', '.join(unknown)}, left for the coordinator")
                continue
            waiting = [d for d in needed if not is_done(by_task[d])]

        if waiting:
            print(f"#{issue['number']} {task}: waits for {', '.join(waiting)}")
            continue

        print(f"#{issue['number']} {task}: ready")
        if not DRY_RUN:
            gh("issue", "edit", str(issue["number"]), "--remove-label", "blocked", "--add-label", "ready")
            gh("issue", "comment", str(issue["number"]), "--body",
               f"Unblocked automatically: dependencies done ({depends}), no gate pending. Moved to `ready`.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
