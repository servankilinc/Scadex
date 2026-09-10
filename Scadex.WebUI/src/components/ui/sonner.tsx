import { Toaster as Sonner, type ToasterProps } from 'sonner';
import { CircleCheckIcon, InfoIcon, TriangleAlertIcon, OctagonXIcon, Loader2Icon } from 'lucide-react';
import { useAppSelector } from '@/hooks';

const Toaster = ({ ...props }: ToasterProps) => {
  // shadcn sablonu burada next-themes'in useTheme()'ini kullaniyordu; uygulamada
  // NextThemesProvider yok, tema layouts/base.tsx'te Redux'tan kuruluyor. Ayni
  // kaynaktan okumazsak toast'lar uygulama temasini hicbir zaman izlemez.
  const theme = useAppSelector(s => s.theme.activeTheme);

  return (
    <Sonner
      theme={theme}
      className='toaster group'
      icons={{
        success: <CircleCheckIcon className='size-4' />,
        info: <InfoIcon className='size-4' />,
        warning: <TriangleAlertIcon className='size-4' />,
        error: <OctagonXIcon className='size-4' />,
        loading: <Loader2Icon className='size-4 animate-spin' />
      }}
      style={
        {
          '--normal-bg': 'var(--popover)',
          '--normal-text': 'var(--popover-foreground)',
          '--normal-border': 'var(--border)',
          '--border-radius': 'var(--radius)'
        } as React.CSSProperties
      }
      toastOptions={{
        classNames: {
          toast: 'cn-toast'
        }
      }}
      {...props}
    />
  );
};

export { Toaster };
