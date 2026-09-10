import { useEffect } from 'react';
import { Outlet } from 'react-router';
import { Toaster } from '@/components/ui/sonner';
import { useAppSelector } from '@/hooks';

export default function BaseLayout() {
  const initialLoading = useAppSelector(s => s.theme.initialLoading);

  // Show nothing while loading
  if (initialLoading) {
    return (
      <ThemeProvider>
        <div className='flex min-h-screen items-center justify-center'>
          <div className='size-6 animate-spin rounded-full border-2 border-primary border-t-transparent' />
        </div>
        <Toaster />
      </ThemeProvider>
    );
  }

  return (
    <ThemeProvider>
      <Outlet />
      <Toaster />
    </ThemeProvider>
  );
}

function ThemeProvider({ children }: { children: React.ReactNode }) {
  const theme = useAppSelector(state => state.theme.activeTheme);

  useEffect(() => {
    const root = window.document.documentElement;
    root.classList.remove('light', 'dark');

    if (theme != 'system') {
      root.classList.add(theme);
      return;
    }

    const systemTheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    root.classList.add(systemTheme);
  }, [theme]);

  return <>{children}</>;
}