import { type NavItem } from './types.ts'

export function isNavItemActive(href: string, item: NavItem) {
  const pathname = href.split(/[?#]/, 1)[0]

  if (item.items) {
    return item.items.some((child) => pathMatches(pathname, String(child.url)))
  }

  return pathMatches(pathname, String(item.url))
}

function pathMatches(pathname: string, itemPath: string) {
  return (
    pathname === itemPath ||
    (itemPath !== '/' && pathname.startsWith(`${itemPath}/`))
  )
}
