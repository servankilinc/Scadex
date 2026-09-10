import { Outlet } from 'react-router';
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar';
import { Separator } from '@/components/ui/separator';
import { AppSidebar } from '@/components/custom/app-sidebar';
import AppBreadcrumb from '@/components/custom/app-breadcrumb';

/**
 * Uygulamanın TEK layout'u.
 *
 * Daha önce `app.tsx` ve `admin.tsx` diye byte-byte aynı iki dosya vardı ve
 * ikisi de `page`/`links` prop'ları bekliyordu — router bunları hiçbir zaman
 * geçmediği için breadcrumb her sayfada "Home / current page"te donmuştu.
 * Breadcrumb artık route `handle.crumb`'larından türüyor, dolayısıyla layout'un
 * prop alması gerekmiyor ve ikinci bir kopyaya da gerek kalmıyor.
 */
export default function AppLayout() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        {/* min-h-0: diyagram gibi tam yukseklik isteyen sayfalar icin sart;
            aksi halde flex cocugu icerigi kadar buyuyup canvas'i tasirir. */}
        <div className='flex min-h-0 flex-1 flex-col'>
          <header className='flex h-16 shrink-0 items-center gap-2 border-b'>
            <div className='flex items-center gap-2 px-4'>
              <SidebarTrigger className='-ml-1' />
              <Separator orientation='vertical' className='mr-2 data-[orientation=vertical]:h-4' />
              <AppBreadcrumb />
            </div>
          </header>
          {/* flex kolonu: diyagram gibi tam yukseklik isteyen sayfalar yuzde
              tabanli yukseklik (h-full / calc(100svh-4rem)) kullanmak zorunda
              kalmasin. Yuzde yukseklik, ust kapsayici yalnizca min-h tasidiginda
              guvenilir cozulmuyor ve canvas 0px yukseklikte render olabiliyor. */}
          <main className='flex min-h-0 flex-1 flex-col overflow-auto'>
            <Outlet />
          </main>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}
