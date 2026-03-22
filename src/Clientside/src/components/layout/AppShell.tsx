import type { ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import logo from '../../DevExSml.png';

const TABS = [
  { name: 'Test Runs', path: '/test-runs' },
  { name: 'API Build', path: '/api-build' },
  { name: 'Clientside Build', path: '/clientside-build' },
];

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <div className="min-h-screen" style={{ backgroundColor: '#f4f6f9' }}>
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30">
        <div className="max-w-screen-2xl mx-auto px-8">
          <div className="flex items-center gap-3 pt-4 pb-3">
            <img src={logo} alt="DX" className="h-8 w-8 object-contain" />
            <span className="text-[17px] font-semibold tracking-tight text-slate-800">DX</span>
          </div>
          <nav className="flex gap-1 -mb-px">
            {TABS.map((t) => {
              const isActive = location.pathname.startsWith(t.path);
              return (
                <button
                  key={t.path}
                  onClick={() => navigate(t.path)}
                  className={`px-4 py-2.5 text-[14px] font-medium transition-colors border-b-2 ${
                    isActive
                      ? 'border-brand-500 text-brand-600'
                      : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
                  }`}
                >
                  {t.name}
                </button>
              );
            })}
          </nav>
        </div>
      </header>
      <main className="px-8 py-6 max-w-screen-2xl mx-auto">{children}</main>
    </div>
  );
}
