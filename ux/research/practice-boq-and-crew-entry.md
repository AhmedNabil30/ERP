# Practice notes: building a BOQ from a catalogue, and entering a crew's hours on a phone

Fetched through web search on 2026-11-10 by the journey-analyst. The search tool returned summaries, so
no page below was opened in full. Treat each line as "the vendor's documentation says so", and open the
page before building on it. Earlier notes on the same subjects are in section 3 of
`ux/journey-project-module-2026-11-09.md` (Procore estimate copy, Odoo project templates, crew time).

## Start a quote from a saved template

- Odoo sales quotation templates: a template holds product lines, section headers and notes. Picking the
  template on a new quotation fills the lines. Source:
  https://www.odoo.com/documentation/18.0/applications/sales/sales/sales_quotations/quote_template.html
- A third-party module's description says standard Odoo does not record which order lines came from a
  template (https://apps.odoo.com/apps/modules/18.0/fal_quotation_template_sale). Third-party claim, not
  Odoo's own.
- Fit for Kaff: a template is a saved list of items, not a project. Nabil remembers "a base project he can
  use and edit". That could be either a template or a copy of a past job. Kaff has not said which (Q-A).

## Add items to an estimate

- Procore (already in the journey file, section 3): items come from a cost catalog by ticking
  checkboxes; drag is for reorder.
- PlanSwift and Planformer market drag-and-drop of pre-built assemblies onto takeoff items on a plan
  (https://hub.planswift.com/construction-takeoff-software-b,
  https://www.getapp.co.uk/software/2090084/planformer). Vendor marketing and a review aggregator.
  Those tools drop onto a measured plan, not into a list. The pattern is different from dragging a row
  into a BOQ list.
- Conclusion: drag from a catalogue into a list has no documentation I could cite as a main method in a
  construction ERP. Nabil asked for it, so it is his direction, tagged PROPOSAL in the briefs, with
  tap-to-add and multi-select as the equal path.

## Enter hours for a crew on a phone

- Procore mobile Timesheets: one timesheet can take a whole crew, and bulk entry applies the same time
  to several people at once. It works offline and syncs later. Temporary labourers can be added without a
  company email or employee number. Source:
  https://support.procore.com/procore-mobile-ios/user-guide/timesheets-ios/tutorials/bulk-enter-time-entry-ios
  and https://support.procore.com/procore-mobile-android/user-guide/timesheets-android/tutorials/bulk-enter-time-entry-android
- Fit for Kaff: transfers. The engineer is the foreman and the labourers are his crew. Procore's crew is a
  saved group created in another tool. Kaff's nearest equivalent is the project's open engagements.
