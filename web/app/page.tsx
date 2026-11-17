import { Button } from "@/components/ui/button";

export default function Home() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col justify-center gap-6 px-6 py-24">
      <h1 className="text-4xl font-semibold tracking-tight">Credere</h1>
      <p className="text-lg text-muted-foreground">
        Banker workbench. The UI is a placeholder until M3 — see{" "}
        <code className="rounded bg-muted px-1 py-0.5 text-sm">
          docs/roadmap.md
        </code>{" "}
        for the milestone plan.
      </p>
      <div className="flex gap-3">
        <Button>Primary action</Button>
        <Button variant="outline">Secondary</Button>
      </div>
    </main>
  );
}
