import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import Link from "next/link";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Mapping Config Studio",
  description: "Configure input layouts and mapping transformations.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={`${geistSans.variable} ${geistMono.variable}`}>
      <body className="min-h-screen bg-slate-50 text-slate-950 antialiased">
        <header className="border-b border-slate-200 bg-white">
          <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4 lg:px-8">
            <Link className="group flex items-center gap-3" href="/configs">
              <span className="grid size-9 place-items-center rounded-xl bg-indigo-600 text-sm font-bold text-white shadow-sm shadow-indigo-200">
                M
              </span>
              <span>
                <span className="block text-sm font-semibold tracking-tight text-slate-950">
                  Mapping Config Studio
                </span>
                <span className="block text-xs text-slate-500">
                  Input and transformation control plane
                </span>
              </span>
            </Link>
            <nav className="flex items-center gap-2" aria-label="Primary">
              <Link
                className="rounded-lg px-3 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-100 hover:text-slate-950"
                href="/configs"
              >
                Configurations
              </Link>
              <Link
                className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-indigo-700"
                href="/configs/new"
              >
                New config
              </Link>
            </nav>
          </div>
        </header>
        {children}
      </body>
    </html>
  );
}
