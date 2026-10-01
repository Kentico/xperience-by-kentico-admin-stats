import { CellType, ColumnContentType, TableCell, TableColumn } from '@kentico/xperience-admin-components';

/** Plain text column for the admin `Table` (not sortable, not searchable). */
export function column(name: string, caption: string, minWidth: number, maxWidth: number): TableColumn {
  return {
    name,
    caption,
    visible: true,
    minWidth,
    maxWidth,
    contentType: ColumnContentType.Text,
    sortable: false,
    searchable: false,
  };
}

/** Plain single-line string cell for the admin `Table`. */
export function stringCell(columnName: string, value: string): TableCell {
  return { type: CellType.String, columnName, value } as TableCell;
}
