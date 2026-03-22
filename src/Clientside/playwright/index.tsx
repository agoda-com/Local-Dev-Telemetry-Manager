import { beforeMount } from '@playwright/experimental-ct-react/hooks';
import { BrowserRouter, MemoryRouter, Routes, Route } from 'react-router-dom';
import '../src/theme/tailwind-tokens.css';

type HooksConfig = {
  enableRouting?: boolean;
  routePath?: string;
  routeUrl?: string;
};

beforeMount<HooksConfig>(async ({ App, hooksConfig }) => {
  if (hooksConfig?.routePath && hooksConfig?.routeUrl) {
    return (
      <MemoryRouter initialEntries={[hooksConfig.routeUrl]}>
        <Routes>
          <Route path={hooksConfig.routePath} element={<App />} />
        </Routes>
      </MemoryRouter>
    );
  }
  if (hooksConfig?.enableRouting) {
    return <BrowserRouter><App /></BrowserRouter>;
  }
});
