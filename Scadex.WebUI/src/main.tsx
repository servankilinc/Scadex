import { StrictMode } from 'react';
import ReactDOM from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import '@/styles/index.css';

// #region Layouts
import BaseLayout from './layouts/base.tsx';
import AppLayout from './layouts/app.tsx';
// #endregion

// #region Views
import AppViews from './views/app';
import AdminViews from './views/admin';
import AuthViews from './views/auth';
import ErrorViews from './views/Error';
// #endregion

import { Provider } from 'react-redux';
import { store } from '@/store/index.ts';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ApiError } from '@/lib/axios-helper.ts';
import RequireAuth from '@/components/custom/require-auth.tsx';
import { requirePermission } from '@/lib/route-guards.ts';
import { enabledModules } from '@/modules';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        // 4xx tekrar denemekle duzelmez; 401'de interceptor oturumu zaten
        // temizledi, buradan tekrar denemek yalnizca gurultu uretir.
        // Transport katmani her hatayi ApiError'a cevirdigi icin AxiosError
        // tanimaya gerek yok.
        if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
        return failureCount < 2;
      }
    },
    mutations: {
      retry: false
    }
  }
});

const router = createBrowserRouter([
  {
    Component: BaseLayout,
    ErrorBoundary: ErrorViews.RouteErrorPage,
    children: [
      // #region (1) App-Layer
      {
        Component: RequireAuth,
        children: [
          {
            path: '/',
            // Tek layout: app.tsx ve admin.tsx byte-byte aynıydı, admin.tsx silindi.
            Component: AppLayout,
            children: [
              { index: true, Component: AppViews.Home },
              { path: 'about', Component: AppViews.About, handle: { crumb: 'Hakkında' } },
              // Kendi hesabı: izin İSTEMEZ (sunucu kimliği token'dan okur). Menüde değil, kullanıcı açılır menüsündedir.
              { path: 'profile', Component: AppViews.Profile, handle: { crumb: 'Profilim' } },
              {
                // Diyagram, /automations ALTINA yuvalanır: breadcrumb eşleşen route
                // zincirinden türediği için, kardeş olsaydı "Otomasyonlar / Diyagram"
                // yerine yalnızca "Diyagram" görünürdü.
                path: 'automations',
                handle: { crumb: 'Otomasyonlar' },
                children: [
                  { index: true, Component: AppViews.Automations },
                  {
                    // Lazy: @xyflow/react agir bir bagimlilik. Route seviyesinde
                    // bolunmezse editore hic girmeyen kullanici da onu indirir.
                    path: ':automationId/diagram',
                    handle: { crumb: 'Diyagram' },
                    loader: requirePermission('ViewDiagram'),
                    lazy: async () => ({ Component: (await import('./views/app/diagram')).default })
                  }
                ]
              },
              {
                // Olay gecmisi barrel'da: agir bagimliligi yok, duz bir tablo.
                path: 'events',
                Component: AppViews.ChannelEvents,
                handle: { crumb: 'Olay Geçmişi' }
              },
              // Kamera ekranları (/cameras) CameraManagement modülündedir (modules/cameramanagement).
              // #endregion

              // #region (1b) Modüller ve çözümler
              // Tek birleştirme noktası: `VITE_MODULES` ile açık modüllerin rotaları. Ekranları
              // modülün kendi manifestosunda `lazy` — kapalı modül ana pakete ekran kodu taşımaz.
              ...enabledModules.flatMap(module => module.routes),
              // #endregion

              // #region (2) Admin-Layer
              {
                path: 'admin',
                handle: { crumb: 'Yönetim' },
                children: [
                  { index: true, Component: AdminViews.Home },
                  // Her ekran yazdığı uçların izniyle korunur (sunucu aynı izni 403 ile zorlar).
                  { path: 'companies', Component: AdminViews.Companies, handle: { crumb: 'Firmalar' }, loader: requirePermission('ConfigureSystem') },
                  { path: 'users', Component: AdminViews.Users, handle: { crumb: 'Kullanıcılar' }, loader: requirePermission('ManageUsers') },
                  { path: 'roles', Component: AdminViews.Roles, handle: { crumb: 'Roller' }, loader: requirePermission('ManageUsers') },
                  { path: 'templates', Component: AdminViews.ComponentTemplates, handle: { crumb: 'Şablonlar' }, loader: requirePermission('ConfigureSystem') }
                  // Kamera çekim ayarları (eski /admin/settings) CameraManagement modülünde: /cameras/settings.
                ]
              }
              // #endregion
            ]
          }
        ]
      },

      // #region (3) Auth-Layer (acik) — anonim kayıt (/signup) bilerek yok: kullanıcıyı yönetici açar.
      { path: '/login', Component: AuthViews.Login }
      // #endregion
    ]
  },
  {
    path: '*',
    Component: ErrorViews.NotFoundPage,
    ErrorBoundary: ErrorViews.RouteErrorPage
  }
]);

ReactDOM.createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </Provider>
  </StrictMode>
);
