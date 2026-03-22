import { test, expect } from '@playwright/experimental-ct-react';
import { formatMs, formatPercent } from './format';

test.describe('formatMs', () => {
  test('returns em dash for null', () => {
    expect(formatMs(null)).toBe('—');
  });

  test('returns em dash for undefined', () => {
    expect(formatMs(undefined)).toBe('—');
  });

  test('returns ms for values under 1000', () => {
    expect(formatMs(500)).toBe('500ms');
  });

  test('returns seconds for values between 1000 and 60000', () => {
    expect(formatMs(1500)).toBe('1.5s');
  });

  test('returns minutes for values at or above 60000', () => {
    expect(formatMs(90000)).toBe('1.5m');
  });

  test('rounds millisecond values to nearest integer', () => {
    expect(formatMs(499.7)).toBe('500ms');
  });

  test('handles zero', () => {
    expect(formatMs(0)).toBe('0ms');
  });

  test('handles exactly 1000ms as seconds', () => {
    expect(formatMs(1000)).toBe('1.0s');
  });

  test('handles exactly 60000ms as minutes', () => {
    expect(formatMs(60000)).toBe('1.0m');
  });
});

test.describe('formatPercent', () => {
  test('returns em dash for null', () => {
    expect(formatPercent(null)).toBe('—');
  });

  test('returns em dash for undefined', () => {
    expect(formatPercent(undefined)).toBe('—');
  });

  test('formats decimal rate as percentage', () => {
    expect(formatPercent(0.953)).toBe('95.3%');
  });

  test('handles zero', () => {
    expect(formatPercent(0)).toBe('0.0%');
  });

  test('handles 1.0 as 100%', () => {
    expect(formatPercent(1)).toBe('100.0%');
  });

  test('handles small fractions', () => {
    expect(formatPercent(0.001)).toBe('0.1%');
  });
});
