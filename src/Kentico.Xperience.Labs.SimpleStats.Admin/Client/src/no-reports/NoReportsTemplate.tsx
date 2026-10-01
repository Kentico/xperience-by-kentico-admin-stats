import { Callout, CalloutPlacementType, CalloutType } from '@kentico/xperience-admin-components';
import React from 'react';

import '../shared/stats.css';

/** Shown when the role may open Simple Stats (Labs) but has no permission for any report. */
export const NoReportsTemplate = () => (
  <div className="SimpleStats-root">
    <Callout
      type={CalloutType.FriendlyWarning}
      placement={CalloutPlacementType.OnDesk}
      headline="No reports available"
    >
      <p>
        Your role has no permission for any Simple Stats (Labs) report. Ask an administrator to add a report
        permission to your role in Role management.
      </p>
    </Callout>
  </div>
);
