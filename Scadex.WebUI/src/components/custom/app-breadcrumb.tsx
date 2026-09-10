import { Fragment } from 'react';
import { Link, useMatches } from 'react-router';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';

/**
 * Breadcrumb'i ROUTE AGACINDAN türetir.
 *
 * Önceden layout bileşeni `page`/`links` prop'ları alıyordu ama router bunları
 * hiçbir zaman geçmiyordu; sonuç, her sayfada donmuş bir "Home / current page"
 * idi. Artık her route kendi etiketini `handle.crumb` ile taşır ve breadcrumb
 * eşleşen route zincirinden kendiliğinden oluşur.
 */

/** Route'un `handle` alanına konan sözleşme: `{ crumb: 'Kabinler' }` */
export interface CrumbHandle {
  crumb: string;
}

function hasCrumb(handle: unknown): handle is CrumbHandle {
  return typeof handle === 'object' && handle !== null && 'crumb' in handle && typeof (handle as CrumbHandle).crumb === 'string';
}

export default function AppBreadcrumb() {
  const matches = useMatches();

  const crumbs = matches.filter(match => hasCrumb(match.handle)).map(match => ({ label: (match.handle as CrumbHandle).crumb, to: match.pathname }));

  if (crumbs.length === 0) return null;

  return (
    <Breadcrumb>
      <BreadcrumbList>
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1;
          return (
            <Fragment key={crumb.to}>
              <BreadcrumbItem>
                {isLast ? (
                  <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                ) : (
                  // <Link> ile: ham <a href> router'i baypas edip tam sayfa
                  // yenileme yapardi ve tum uygulama state'i sifirlanirdi.
                  <BreadcrumbLink render={<Link to={crumb.to} />}>{crumb.label}</BreadcrumbLink>
                )}
              </BreadcrumbItem>
              {!isLast && <BreadcrumbSeparator className='hidden md:block' />}
            </Fragment>
          );
        })}
      </BreadcrumbList>
    </Breadcrumb>
  );
}
