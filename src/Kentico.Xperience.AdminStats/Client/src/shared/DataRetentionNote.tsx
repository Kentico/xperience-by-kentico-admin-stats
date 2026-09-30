import {
  Callout,
  CalloutPlacementType,
  CalloutType,
} from '@kentico/xperience-admin-components';
import React, { ReactNode } from 'react';

const inactiveContactsDocsUrl = 'https://docs.kentico.com/x/delete_inactive_contacts_xp';

export interface DataRetentionNoteProps {
  /** Optional report-specific sentence shown after the general note. */
  readonly children?: ReactNode;
}

/** Explains that cleanup tasks delete old data, so drops in trends can be deletions. */
export const DataRetentionNote = ({ children }: DataRetentionNoteProps) => (
  <Callout
    type={CalloutType.QuickTip}
    placement={CalloutPlacementType.OnDesk}
    headline="Data retention"
  >
    <p>
      Contact and activity cleanup deletes old data based on your settings. A drop
      in older periods can mean data was deleted, not that activity went down.
      {children && <> {children}</>}
    </p>
    <p>
      {/* Plain anchor: Callout styles links in its content with the admin link colors. */}
      <a href={inactiveContactsDocsUrl} target="_blank" rel="noopener noreferrer">
        Learn how inactive contacts are deleted
      </a>
    </p>
  </Callout>
);
