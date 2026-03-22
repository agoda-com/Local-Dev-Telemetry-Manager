export interface FilterParams {
  environment?: string;
  platform?: string;
  projectName?: string;
  repository?: string;
  branch?: string;
  testRunner?: string;
  metricType?: string;
  dateFrom?: string;
  dateTo?: string;
  dateRange?: string;
  page?: number;
  pageSize?: number;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TestRunSummary {
  totalRuns: number;
  avgDurationMs: number;
  passRate: number;
  durationTrend: DailyDataPoint[];
  passFailTrend: DailyPassFail[];
}

export interface DailyDataPoint {
  date: string;
  value: number;
}

export interface DailyPassFail {
  date: string;
  passed: number;
  failed: number;
}

export interface TestRunItem {
  id: string;
  receivedAt: string;
  projectName: string;
  testRunner: string;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  totalDurationMs: number;
  executionEnvironment: string;
}

export interface TestRunDetail {
  id: string;
  runId: string;
  receivedAt: string;
  userName: string;
  hostname: string;
  branch: string;
  projectName: string;
  testRunner: string;
  executionEnvironment: string;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  skippedTests: number;
  totalDurationMs: number;
  testCases: TestCaseItem[];
}

export interface TestCaseItem {
  name: string;
  fullName: string | null;
  className: string | null;
  status: string;
  durationMs: number | null;
  errorMessage: string | null;
}

export interface ApiBuildSummary {
  avgCompileTimeMs: number;
  avgStartupTimeMs: number;
  avgFirstResponseTimeMs: number;
  compileTrend: DailyDataPoint[];
  startupTrend: DailyDataPoint[];
  firstResponseTrend: DailyDataPoint[];
  dailyLifecycle: DailyLifecyclePoint[];
}

export interface DailyLifecyclePoint {
  date: string;
  avgCompileMs: number;
  avgStartupMs: number;
  avgFirstResponseMs: number;
}

export interface ClientsideBuildSummary {
  hotReloadPercent: number;
  avgHotReloadTimeMs: number;
  avgFullReloadTimeMs: number;
  totalReloadsToday: number;
  reloadCountTrend: DailyReloadCount[];
  reloadTimeTrend: DailyReloadTime[];
}

export interface DailyReloadCount {
  date: string;
  hotReloads: number;
  fullReloads: number;
}

export interface DailyReloadTime {
  date: string;
  avgHotTimeMs: number;
  avgFullTimeMs: number;
}

export interface BuildMetricItem {
  id: string;
  receivedAt: string;
  projectName: string;
  metricType: string;
  buildCategory: string;
  reloadType: string | null;
  timeTakenMs: number;
  executionEnvironment: string;
}

export interface FilterOptions {
  projects: string[];
  repositories: string[];
  branches: string[];
  testRunners: string[];
  metricTypes: string[];
}

const BASE_URL = '';

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(`${BASE_URL}${url}`);
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

function buildQuery(params: FilterParams): string {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      query.set(key, String(value));
    }
  });
  const str = query.toString();
  return str ? `?${str}` : '';
}

export async function fetchTestRunSummary(filters: FilterParams): Promise<TestRunSummary> {
  return fetchJson<TestRunSummary>(`/api/test-runs/summary${buildQuery(filters)}`);
}

export async function fetchTestRuns(filters: FilterParams): Promise<PaginatedResult<TestRunItem>> {
  return fetchJson<PaginatedResult<TestRunItem>>(`/api/test-runs${buildQuery(filters)}`);
}

export async function fetchTestRunDetail(id: string): Promise<TestRunDetail> {
  return fetchJson<TestRunDetail>(`/api/test-runs/${id}`);
}

export async function fetchApiBuildSummary(filters: FilterParams): Promise<ApiBuildSummary> {
  return fetchJson<ApiBuildSummary>(`/api/build-metrics/api-summary${buildQuery(filters)}`);
}

export async function fetchClientsideBuildSummary(filters: FilterParams): Promise<ClientsideBuildSummary> {
  return fetchJson<ClientsideBuildSummary>(`/api/build-metrics/clientside-summary${buildQuery(filters)}`);
}

export async function fetchBuildMetrics(filters: FilterParams): Promise<PaginatedResult<BuildMetricItem>> {
  return fetchJson<PaginatedResult<BuildMetricItem>>(`/api/build-metrics${buildQuery(filters)}`);
}

export async function fetchFilters(): Promise<FilterOptions> {
  return fetchJson<FilterOptions>('/api/filters');
}
