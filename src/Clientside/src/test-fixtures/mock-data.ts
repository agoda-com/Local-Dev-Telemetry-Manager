import type {
  FilterOptions,
  TestRunSummary,
  TestRunItem,
  PaginatedResult,
  TestRunDetail,
  ApiBuildSummary,
  ClientsideBuildSummary,
  BuildMetricItem,
} from '../api/client';

export const mockFilterOptions: FilterOptions = {
  projects: ['ProjectA', 'ProjectB'],
  repositories: ['repo-alpha', 'repo-beta'],
  branches: ['main', 'develop'],
  platforms: ['Win32NT', 'Unix'],
  testRunners: ['NUnit', 'xUnit'],
  metricTypes: ['API', 'Clientside'],
};

export const mockTestRunSummary: TestRunSummary = {
  totalRuns: 142,
  avgDurationMs: 34520,
  passRate: 0.953,
  durationTrend: [
    { date: '2026-03-15', value: 32000 },
    { date: '2026-03-16', value: 35000 },
    { date: '2026-03-17', value: 33000 },
  ],
  passFailTrend: [
    { date: '2026-03-15', passed: 45, failed: 2 },
    { date: '2026-03-16', passed: 50, failed: 3 },
    { date: '2026-03-17', passed: 47, failed: 1 },
  ],
};

export const mockTestRunItems: PaginatedResult<TestRunItem> = {
  items: [
    {
      id: 'run-1',
      receivedAt: '2026-03-17T10:30:00Z',
      projectName: 'ProjectA',
      testRunner: 'NUnit',
      totalTests: 100,
      passedTests: 98,
      failedTests: 2,
      totalDurationMs: 34520,
      executionEnvironment: 'Local',
    },
    {
      id: 'run-2',
      receivedAt: '2026-03-16T14:20:00Z',
      projectName: 'ProjectB',
      testRunner: 'xUnit',
      totalTests: 75,
      passedTests: 75,
      failedTests: 0,
      totalDurationMs: 22100,
      executionEnvironment: 'CI',
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
};

export const mockTestRunDetail: TestRunDetail = {
  id: 'run-1',
  runId: 'abc-123',
  receivedAt: '2026-03-17T10:30:00Z',
  userName: 'jdickson',
  hostname: 'dev-machine',
  branch: 'feature/testing',
  projectName: 'ProjectA',
  testRunner: 'NUnit',
  executionEnvironment: 'Local',
  totalTests: 5,
  passedTests: 4,
  failedTests: 1,
  skippedTests: 0,
  totalDurationMs: 34520,
  testCases: [
    {
      name: 'TestOne',
      fullName: 'Ns.TestOne',
      className: 'TestClass',
      status: 'Passed',
      durationMs: 1200,
      errorMessage: null,
    },
    {
      name: 'TestTwo',
      fullName: 'Ns.TestTwo',
      className: 'TestClass',
      status: 'Passed',
      durationMs: 800,
      errorMessage: null,
    },
    {
      name: 'TestThree',
      fullName: 'Ns.TestThree',
      className: 'TestClass',
      status: 'Failed',
      durationMs: 500,
      errorMessage: 'Expected true but got false',
    },
    {
      name: 'TestFour',
      fullName: 'Ns.TestFour',
      className: 'OtherClass',
      status: 'Passed',
      durationMs: 15000,
      errorMessage: null,
    },
    {
      name: 'TestFive',
      fullName: 'Ns.TestFive',
      className: 'OtherClass',
      status: 'Passed',
      durationMs: 3200,
      errorMessage: null,
    },
  ],
};

export const mockApiBuildSummary: ApiBuildSummary = {
  avgCompileTimeMs: 12500,
  avgStartupTimeMs: 3200,
  avgFirstResponseTimeMs: 800,
  compileTrend: [
    { date: '2026-03-15', value: 12000 },
    { date: '2026-03-16', value: 13000 },
    { date: '2026-03-17', value: 12500 },
  ],
  startupTrend: [
    { date: '2026-03-15', value: 3100 },
    { date: '2026-03-16', value: 3300 },
    { date: '2026-03-17', value: 3200 },
  ],
  firstResponseTrend: [
    { date: '2026-03-15', value: 750 },
    { date: '2026-03-16', value: 850 },
    { date: '2026-03-17', value: 800 },
  ],
  dailyLifecycle: [
    { date: '2026-03-15', avgCompileMs: 12000, avgStartupMs: 3100, avgFirstResponseMs: 750 },
    { date: '2026-03-16', avgCompileMs: 13000, avgStartupMs: 3300, avgFirstResponseMs: 850 },
    { date: '2026-03-17', avgCompileMs: 12500, avgStartupMs: 3200, avgFirstResponseMs: 800 },
  ],
};

export const mockClientsideBuildSummary: ClientsideBuildSummary = {
  hotReloadPercent: 85,
  avgHotReloadTimeMs: 450,
  avgFullReloadTimeMs: 3200,
  totalReloadsToday: 42,
  reloadCountTrend: [
    { date: '2026-03-15', hotReloads: 35, fullReloads: 8 },
    { date: '2026-03-16', hotReloads: 40, fullReloads: 5 },
    { date: '2026-03-17', hotReloads: 42, fullReloads: 6 },
  ],
  reloadTimeTrend: [
    { date: '2026-03-15', avgHotTimeMs: 420, avgFullTimeMs: 3100 },
    { date: '2026-03-16', avgHotTimeMs: 460, avgFullTimeMs: 3300 },
    { date: '2026-03-17', avgHotTimeMs: 450, avgFullTimeMs: 3200 },
  ],
};

export const mockBuildMetricItems: PaginatedResult<BuildMetricItem> = {
  items: [
    {
      id: 'bm-1',
      receivedAt: '2026-03-17T10:30:00Z',
      projectName: 'ProjectA',
      metricType: 'API',
      buildCategory: 'Debug',
      reloadType: null,
      timeTakenMs: 12500,
      executionEnvironment: 'Local',
    },
    {
      id: 'bm-2',
      receivedAt: '2026-03-16T14:20:00Z',
      projectName: 'ProjectB',
      metricType: 'Clientside',
      buildCategory: 'Development',
      reloadType: 'Hot',
      timeTakenMs: 450,
      executionEnvironment: 'Local',
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
};
