import React, { ComponentType, createContext, useContext } from 'react';

/** Client properties shared by all report pages (`StatsReportClientProperties` on the server). */
export interface StatsReportProps {
  /** The user has the Export permission. */
  readonly canExport?: boolean;
}

// Hidden unless the server says the user may export.
const ExportPermissionContext = createContext(false);

/** Whether "Export CSV" buttons are shown. */
export const useCanExport = () => useContext(ExportPermissionContext);

/**
 * Wraps a report template so its tiles know whether the user has the Export permission.
 * UI guard only: the CSV is built from data already on the page.
 */
export function withExportPermission<P extends object>(Template: ComponentType<P>) {
  const WithExportPermission = (props: P & StatsReportProps) => (
    <ExportPermissionContext.Provider value={props.canExport === true}>
      <Template {...props} />
    </ExportPermissionContext.Provider>
  );
  WithExportPermission.displayName = `WithExportPermission(${Template.displayName ?? Template.name})`;
  return WithExportPermission;
}
