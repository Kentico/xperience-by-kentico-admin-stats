import { Table, TableColumn, TableRow } from '@kentico/xperience-admin-components';
import React, { useMemo } from 'react';

import { formatValue, numberFormat } from '../shared/format';
import { adminLinkCell } from '../shared/RankedTable';
import { column, stringCell } from '../shared/table';
import type { EmailSummaryAutomatedEmail, EmailSummaryEmail } from './EmailSummaryTemplate';

/** Send time as stored on the server (`yyyy-MM-ddTHH:mm:ss`), shown as `yyyy-MM-dd HH:mm` without a time zone shift. */
export function formatSendTime(value: string): string {
  return value.slice(0, 16).replace('T', ' ');
}

/** A nullable count (bounces, spam reports): "–" when the delivery provider does not track it. */
function formatOptional(value: number | null): string {
  return value === null ? '–' : numberFormat.format(value);
}

export interface EmailTableProps {
  readonly emails: readonly EmailSummaryEmail[];
  /** Shows the hard bounces column (hidden when no email has them). */
  readonly showHardBounces: boolean;
  /** Link to the email's Statistics tab, or `null`. */
  readonly getAdminHref: (path: string | null) => string | null;
}

/**
 * Regular emails sent in the range as a native admin table: name (link to the Statistics tab), send date and lifetime numbers.
 * The shared ranked table has up to three values, so this list has its own table built from the same cells.
 */
export const EmailTable = ({ emails, showHardBounces, getAdminHref }: EmailTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', 'Email', 24, 60),
      column('sendTime', 'Send date', 14, 18),
      column('sent', 'Sent', 8, 12),
      column('delivered', 'Delivered', 8, 12),
      column('openRate', 'Open rate', 8, 12),
      column('clickRate', 'Click rate', 8, 12),
      ...(showHardBounces ? [column('hardBounces', 'Hard bounces', 10, 14)] : []),
      column('unsubscribes', 'Unsubscribes', 10, 14),
    ],
    [showHardBounces],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      emails.map((email) => ({
        identifier: String(email.id),
        disabled: false,
        cells: [
          adminLinkCell('label', email.name, getAdminHref(email.statisticsPath)),
          stringCell('sendTime', formatSendTime(email.sendTime)),
          stringCell('sent', email.hasStatistics ? numberFormat.format(email.statistics.sent) : '–'),
          stringCell('delivered', email.hasStatistics ? numberFormat.format(email.statistics.delivered) : '–'),
          stringCell('openRate', formatValue(email.rates.openRate, 'Ratio')),
          stringCell('clickRate', formatValue(email.rates.clickRate, 'Ratio')),
          ...(showHardBounces ? [stringCell('hardBounces', formatOptional(email.statistics.hardBounces))] : []),
          stringCell('unsubscribes', email.hasStatistics ? numberFormat.format(email.statistics.unsubscribes) : '–'),
        ],
      })),
    [emails, showHardBounces, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

export interface AutomatedEmailTableProps {
  readonly emails: readonly EmailSummaryAutomatedEmail[];
  /** Link to the email's Statistics tab, or `null`. */
  readonly getAdminHref: (path: string | null) => string | null;
}

/** Automated emails as a native admin table: name (link to the Statistics tab), purpose, sent in range and lifetime numbers. */
export const AutomatedEmailTable = ({ emails, getAdminHref }: AutomatedEmailTableProps) => {
  const columns = useMemo<TableColumn[]>(
    () => [
      column('label', 'Email', 24, 60),
      column('purpose', 'Purpose', 12, 18),
      column('sentInRange', 'Sent in range', 10, 14),
      column('sent', 'Sent (lifetime)', 10, 14),
      column('openRate', 'Open rate', 8, 12),
      column('clickRate', 'Click rate', 8, 12),
    ],
    [],
  );

  const rows = useMemo<TableRow[]>(
    () =>
      emails.map((email) => ({
        identifier: String(email.id),
        disabled: false,
        cells: [
          adminLinkCell('label', email.name, getAdminHref(email.statisticsPath)),
          stringCell('purpose', formatPurpose(email.purpose)),
          stringCell('sentInRange', numberFormat.format(email.sentInRange)),
          stringCell('sent', numberFormat.format(email.statistics.sent)),
          stringCell('openRate', formatValue(email.rates.openRate, 'Ratio')),
          stringCell('clickRate', formatValue(email.rates.clickRate, 'Ratio')),
        ],
      })),
    [emails, getAdminHref],
  );

  return (
    <div className="SimpleStats-tableScroll">
      <Table columns={columns} rows={rows} />
    </div>
  );
};

/** Product purpose code names (`EmailPurpose`) as words, for example `FormAutoresponder` as "Form autoresponder". Unknown values show as stored. */
export function formatPurpose(purpose: string): string {
  const words = purpose.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}
