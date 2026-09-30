/**
 * Turns an admin page path from the server (`IPageLinkGenerator.GetPath`, relative to the admin root,
 * for example `/forms/list/1/submissions`) into a link.
 *
 * The admin path prefix (`/admin` by default, plus any app path base) has no public server API,
 * so it is read from the browser location: the current report page is at `<admin root><pagePath>`.
 * Returns `null` when the location does not end with `pagePath` (the link is then left out).
 */
export function toAdminHref(
  adminPath: string | null | undefined,
  pagePath: string | null | undefined,
  locationPath: string = window.location.pathname,
): string | null {
  if (!adminPath || !pagePath) {
    return null;
  }

  const current = trimTrailingSlash(locationPath);
  const page = trimTrailingSlash(pagePath);

  if (!page || !current.toLowerCase().endsWith(page.toLowerCase())) {
    return null;
  }

  const root = current.slice(0, current.length - page.length);
  return `${root}${adminPath.startsWith('/') ? adminPath : `/${adminPath}`}`;
}

function trimTrailingSlash(path: string): string {
  return path.endsWith('/') ? path.slice(0, -1) : path;
}
