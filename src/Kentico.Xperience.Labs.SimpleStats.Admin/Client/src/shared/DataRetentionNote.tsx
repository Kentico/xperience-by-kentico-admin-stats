import {
  Callout,
  CalloutPlacementType,
  CalloutType,
} from '@kentico/xperience-admin-components';
import React, { ReactNode } from 'react';

const inactiveContactsDocsUrl = 'https://docs.kentico.com/x/delete_inactive_contacts_xp';

export interface DataRetentionNoteLink {
  readonly href: string;
  readonly label: string;
}

export interface DataRetentionNoteProps {
  /** Optional report-specific sentence shown after the general note. */
  readonly children?: ReactNode;
  /** Replaces the general note for reports whose data is not removed by contact cleanup. */
  readonly message?: ReactNode;
  /** Replaces the default docs link. */
  readonly link?: DataRetentionNoteLink;
}

const defaultMessage = (
  <>
    Contact and activity cleanup deletes old data based on your settings. A drop
    in older periods can mean data was deleted, not that activity went down.
  </>
);

const defaultLink: DataRetentionNoteLink = {
  href: inactiveContactsDocsUrl,
  label: 'Learn how inactive contacts are deleted',
};

/** Explains that cleanup deletes old data, so drops in trends can be deletions. */
export const DataRetentionNote = ({
  children,
  message = defaultMessage,
  link = defaultLink,
}: DataRetentionNoteProps) => (
  <Callout
    type={CalloutType.QuickTip}
    placement={CalloutPlacementType.OnDesk}
    headline="Data retention"
  >
    <p>
      {message}
      {children && <> {children}</>}
    </p>
    <p>
      {/* Plain anchor: Callout styles links in its content with the admin link colors. */}
      <a href={link.href} target="_blank" rel="noopener noreferrer">
        {link.label}
      </a>
    </p>
  </Callout>
);
