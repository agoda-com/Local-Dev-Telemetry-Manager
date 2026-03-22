import type { ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { TabGroup, TabList, Tab } from '@tremor/react';

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

  const currentIndex = TABS.findIndex((t) => location.pathname.startsWith(t.path));

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 px-6 pt-5 pb-0">
        <h1 className="text-xl font-semibold text-gray-900 mb-4">DevEx Telemetry</h1>
        <TabGroup
          index={currentIndex >= 0 ? currentIndex : 0}
          onIndexChange={(i) => navigate(TABS[i].path)}
        >
          <TabList variant="line">
            {TABS.map((t) => (
              <Tab key={t.path}>{t.name}</Tab>
            ))}
          </TabList>
        </TabGroup>
      </header>
      <main className="p-6 max-w-screen-2xl mx-auto">{children}</main>
    </div>
  );
}
