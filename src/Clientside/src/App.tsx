import { Routes, Route, Navigate } from 'react-router-dom';
import { AppShell } from './components/layout/AppShell';
import { TestRunDashboard } from './pages/TestRunPerformance/TestRunDashboard';
import { TestRunDetail } from './pages/TestRunPerformance/TestRunDetail';
import { ApiBuildDashboard } from './pages/ApiBuildPerformance/ApiBuildDashboard';
import { ClientsideBuildDashboard } from './pages/ClientsideBuildPerformance/ClientsideBuildDashboard';

export default function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<Navigate to="/test-runs" replace />} />
        <Route path="/test-runs" element={<TestRunDashboard />} />
        <Route path="/test-runs/:id" element={<TestRunDetail />} />
        <Route path="/api-build" element={<ApiBuildDashboard />} />
        <Route path="/clientside-build" element={<ClientsideBuildDashboard />} />
      </Routes>
    </AppShell>
  );
}
