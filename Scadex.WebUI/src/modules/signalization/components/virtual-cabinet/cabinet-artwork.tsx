import { useCallback, useState, type ReactNode } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { useCabinetArtwork } from '../../hooks/use-cabinet-artwork';
import { ArtworkContext, mountArtwork, type ArtworkSources, type MountedArtwork } from './artwork';

/**
 * Kabin çizimi — kasa (`cabinet-inside.svg`) ve cihaz dosyaları OLDUĞU GİBİ kullanılır (bkz. `artwork.ts`).
 *
 * Çocuklar cihaz bileşenleridir (`CameraFigure`, `LedFigure`, `IndoorGrid`…): çizim hazır olduktan SONRA çizilirler, kasadaki
 * kutulara id ile bağlanırlar ve cihazın görünümünü (`<use>` ile sembol) portal üzerinden kutunun çerçevesine koyarlar.
 */
interface CabinetArtworkProps {
  className?: string;
  children?: ReactNode;
}

export function CabinetArtwork({ className, children }: CabinetArtworkProps) {
  const { data: sources, isPending, error } = useCabinetArtwork();

  if (isPending) return <Skeleton className={cn('rounded-xl', className)} />;

  if (error) {
    return <p className={cn('flex items-center justify-center text-sm text-destructive', className)}>{error.message}</p>;
  }

  return (
    <ArtworkHost sources={sources} className={className}>
      {children}
    </ArtworkHost>
  );
}

function ArtworkHost({ sources, className, children }: CabinetArtworkProps & { sources: ArtworkSources }) {
  const [artwork, setArtwork] = useState<MountedArtwork | null>(null);
  const [failure, setFailure] = useState<string | null>(null);

  // Callback ref: çizim React'in değil dosyanın DOM'udur; kap belgeye eklenince içine konur, kap sökülünce çıkarılır.
  const hostRef = useCallback(
    (host: HTMLDivElement | null) => {
      if (!host) return;

      try {
        const mounted = mountArtwork(host, sources);
        setArtwork(mounted);
        return () => mounted.svg.remove();
      } catch (mountError) {
        setFailure(mountError instanceof Error ? mountError.message : 'Kabin çizimi okunamadı.');
        return undefined;
      }
    },
    [sources]
  );

  if (failure) {
    return <p className={cn('flex items-center justify-center text-sm text-destructive', className)}>{failure}</p>;
  }

  return (
    <>
      <div ref={hostRef} className={className} />
      {artwork && <ArtworkContext value={artwork}>{children}</ArtworkContext>}
    </>
  );
}
