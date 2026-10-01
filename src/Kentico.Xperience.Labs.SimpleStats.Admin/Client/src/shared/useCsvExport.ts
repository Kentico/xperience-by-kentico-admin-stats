import { usePageCommandProvider } from '@kentico/xperience-admin-base';
import { useCallback } from 'react';

import { CsvData, downloadCsv } from './csv';

/** Mirrors `StatsExportLogRequest`. */
interface StatsExportLogRequest {
  readonly exportName: string;
  readonly fileName: string;
  readonly rowCount: number;
}

/**
 * Returns `exportCsv`, which downloads the CSV and then reports it with the shared `LOG_EXPORT` command
 * (raises `AfterExportStatsEvent` on the server). The command is not awaited; a failure is only logged to the console.
 *
 * `exportName` is a stable ID of the tile (lowercase letters, digits and hyphens), usually the file-name prefix
 * without the filter part, for example `consents-events`.
 */
export function useCsvExport() {
  const { executeCommand } = usePageCommandProvider();

  return useCallback(
    (exportName: string, fileName: string, csv: CsvData) => {
      downloadCsv(fileName, csv);

      executeCommand<void, StatsExportLogRequest>('LOG_EXPORT', {
        exportName,
        fileName,
        rowCount: csv.rowCount,
      }).catch((error: unknown) => console.error('The CSV export could not be logged.', error));
    },
    [executeCommand],
  );
}
