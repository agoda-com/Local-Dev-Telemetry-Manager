interface PageHeaderProps {
  title: string;
  subtitle?: string;
}

export function PageHeader({ title, subtitle }: PageHeaderProps) {
  return (
    <div className="mb-8">
      <h2 className="text-2xl font-semibold tracking-tight text-slate-700">{title}</h2>
      {subtitle && <p className="mt-1.5 text-sm text-slate-400 font-light">{subtitle}</p>}
    </div>
  );
}
