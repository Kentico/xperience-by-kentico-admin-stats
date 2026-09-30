import { usePageCommandProvider } from '@kentico/xperience-admin-base';
import { useCallback, useRef, useState } from 'react';

import { StatsFilter, StatsLoadRequest } from './types';

export interface StatsLoadOptions {
  /** Bypass the server cache for this filter. */
  readonly refresh?: boolean;
}

export interface StatsCommandState<TResult, TFilter = StatsFilter> {
  readonly data: TResult;
  readonly isLoading: boolean;
  readonly hasError: boolean;
  readonly load: (filter: TFilter, options?: StatsLoadOptions) => Promise<void>;
}

/**
 * Wraps a report page command (default `LOAD`) that takes a `StatsLoadRequest` (or a snapshot request with `TFilter`).
 * Ignores responses from older requests when filters change quickly.
 */
export function useStatsCommand<TResult, TFilter = StatsFilter>(
  initialData: TResult,
  commandName = 'LOAD',
): StatsCommandState<TResult, TFilter> {
  const [data, setData] = useState<TResult>(initialData);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);
  const requestId = useRef(0);

  // `usePageCommand().execute` resolves to void (the result only reaches its `after` callback),
  // so the provider is used directly to get the result back.
  const { executeCommand } = usePageCommandProvider();

  const load = useCallback(
    async (filter: TFilter, options?: StatsLoadOptions) => {
      const id = ++requestId.current;
      setIsLoading(true);
      setHasError(false);

      try {
        const result = await executeCommand<TResult, StatsLoadRequest<TFilter>>(commandName, {
          filter,
          refresh: options?.refresh ?? false,
        });
        if (id !== requestId.current) {
          return;
        }

        if (result === undefined) {
          setHasError(true);
        } else {
          setData(result);
        }
      } catch {
        if (id === requestId.current) {
          setHasError(true);
        }
      } finally {
        if (id === requestId.current) {
          setIsLoading(false);
        }
      }
    },
    [executeCommand, commandName],
  );

  return { data, isLoading, hasError, load };
}
