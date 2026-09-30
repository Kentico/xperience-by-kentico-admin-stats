// Exposes components from the module. All added components need to be exported.
import { ActivityCountsTemplate as ActivityCounts } from './activity-counts/ActivityCountsTemplate';
import { ConsentsTemplate as Consents } from './consents/ConsentsTemplate';
import { ContentInventoryTemplate as ContentInventory } from './content-inventory/ContentInventoryTemplate';
import { CustomersTemplate as Customers } from './customers/CustomersTemplate';
import { EventLogTemplate as EventLog } from './event-log/EventLogTemplate';
import { FormSubmissionsTemplate as FormSubmissions } from './form-submissions/FormSubmissionsTemplate';
import { MembersTemplate as Members } from './members/MembersTemplate';
import { NewContactsTemplate as NewContacts } from './new-contacts/NewContactsTemplate';
import { OrdersRevenueTemplate as OrdersRevenue } from './orders-revenue/OrdersRevenueTemplate';
import { withExportPermission } from './shared/exportPermission';
import { TopPagesTemplate as TopPages } from './top-pages/TopPagesTemplate';

// Report templates get the Export permission (`canExport`) for their "Export CSV" buttons.
export const ActivityCountsTemplate = withExportPermission(ActivityCounts);
export const TopPagesTemplate = withExportPermission(TopPages);
export const NewContactsTemplate = withExportPermission(NewContacts);
export const FormSubmissionsTemplate = withExportPermission(FormSubmissions);
export const ContentInventoryTemplate = withExportPermission(ContentInventory);
export const EventLogTemplate = withExportPermission(EventLog);
export const OrdersRevenueTemplate = withExportPermission(OrdersRevenue);
export const CustomersTemplate = withExportPermission(Customers);
export const MembersTemplate = withExportPermission(Members);
export const ConsentsTemplate = withExportPermission(Consents);

export { NoReportsTemplate } from './no-reports/NoReportsTemplate';
