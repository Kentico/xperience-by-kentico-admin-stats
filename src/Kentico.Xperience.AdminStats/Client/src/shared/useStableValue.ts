import { useRef } from 'react';

/**
 * Returns the same reference as long as the value's content (its JSON) does not change.
 *
 * Charts rebuild their amCharts root when their data changes. Parents often pass new arrays, objects
 * or callbacks with the same content (for example caption objects created in render, or a tile re-rendering
 * after a toggle), so charts compare their derived data by content with this hook and rebuild only on real changes.
 * Use it for small, JSON-serializable data (chart rows, series).
 */
export function useStableValue<T>(value: T): T {
  const ref = useRef<{ readonly key: string; readonly value: T } | null>(null);
  const key = JSON.stringify(value);

  if (ref.current === null || ref.current.key !== key) {
    ref.current = { key, value };
  }

  return ref.current.value;
}
