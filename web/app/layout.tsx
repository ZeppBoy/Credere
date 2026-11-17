import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Credere — banker workbench",
  description:
    "Loan servicing + AI augmentation. Portfolio project. See the repo README for current status.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-background text-foreground antialiased">
        {children}
      </body>
    </html>
  );
}
