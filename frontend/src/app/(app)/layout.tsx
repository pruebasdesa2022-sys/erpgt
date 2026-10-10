import { MarcoApp } from "@/components/layout/MarcoApp";

export default function LayoutApp({ children }: LayoutProps<"/">) {
  return <MarcoApp>{children}</MarcoApp>;
}
