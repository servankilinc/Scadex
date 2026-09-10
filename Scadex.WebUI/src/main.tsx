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
              {
                // Diyagram, /cabinets ALTINA yuvalanır: breadcrumb eşleşen route
                // zincirinden türediği için, kardeş olsaydı "Kabinler / Diyagram"
                // yerine yalnızca "Diyagram" görünürdü.
                path: 'cabinets',
                handle: { crumb: 'Kabinler' },
                children: [
                  { index: true, Component: AppViews.Cabinets },
                  {
                    // Lazy: @xyflow/react agir bir bagimlilik. Route seviyesinde
                    // bolunmezse editore hic girmeyen kullanici da onu indirir.
                    path: ':cabinetId/diagram',
                    handle: { crumb: 'Diyagram' },
                    lazy: async () => ({ Component: (await import('./views/app/diagram')).default })
                  }
                ]
              },
              {
                // Canlı izleme. Diyagramla aynı gerekçeyle LAZY ve barrel'a
                // KONMADI: WebRTC/WHEP kodu, hiç kamera izlemeyen kullanıcının
                // ana paketine girmemeli.
                path: 'cameras',
                handle: { crumb: 'Canlı İzleme' },
                children: [
                  {
                    index: true,
                    lazy: async () => ({ Component: (await import('./views/app/cameras')).default })
                  },
                  {
                    path: ':cameraId',
                    handle: { crumb: 'Kamera' },
                    lazy: async () => ({ Component: (await import('./views/app/cameras/detail')).default })
                  }
                ]
              },
              // #endregion

              // #region (2) Admin-Layer
              {
                path: 'admin',
                handle: { crumb: 'Yönetim' },
                children: [
                  { index: true, Component: AdminViews.Home },
                  { path: 'companies', Component: AdminViews.Companies, handle: { crumb: 'Firmalar' } },
                  { path: 'templates', Component: AdminViews.ComponentTemplates, handle: { crumb: 'Şablonlar' } },
                  { path: 'cameras', Component: AdminViews.Cameras, handle: { crumb: 'Kameralar' } }
                ]
              }
              // #endregion
            ]
          }
        ]
      },

      // #region (3) Auth-Layer (acik)
      { path: '/login', Component: AuthViews.Login },
      { path: '/signup', Component: AuthViews.Signup }
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
