import { Link, isRouteErrorResponse, useRouteError } from 'react-router';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/axios-helper';

export default {
  NotFoundPage,
  RouteErrorPage
};

function NotFoundPage() {
  return (
    <div className='flex min-h-screen items-center px-4 py-12 sm:px-6 md:px-8 lg:px-12 xl:px-16'>
      <div className='w-full space-y-6 text-center'>
        <div className='space-y-3'>
          <h1 className='animate-bounce text-4xl font-bold tracking-tighter sm:text-5xl'>404</h1>
          <p className='text-muted-foreground'>Aradığınız sayfa bulunamadı.</p>
        </div>
        <Button render={<Link to='/' />}>Ana sayfaya dön</Button>
      </div>
    </div>
  );
}


function RouteErrorPage() {
  const error = useRouteError();

  let title = 'Bir şeyler ters gitti';
  let detail = 'Beklenmeyen bir hata oluştu.';

  if (isRouteErrorResponse(error)) {
    title = `${error.status} ${error.statusText}`;
    detail = typeof error.data === 'string' ? error.data : detail;
  } else if (error instanceof ApiError) {
    // Transport katmani her HTTP hatasini ApiError'a ceviriyor; ProblemDetails
    // gövdesinden gelen mesaj burada en anlamli metin.
    title = `Sunucu hatası (${error.status})`;
    detail = error.message;
  } else if (error instanceof Error) {
    detail = error.message;
  }

  return (
    <div className='flex min-h-screen items-center px-4 py-12 sm:px-6 md:px-8 lg:px-12 xl:px-16'>
      <div className='mx-auto w-full max-w-lg space-y-6 text-center'>
        <div className='space-y-3'>
          <h1 className='text-3xl font-bold tracking-tighter sm:text-4xl'>{title}</h1>
          <p className='text-muted-foreground wrap-break-word'>{detail}</p>
        </div>
        <div className='flex justify-center gap-3'>
          <Button variant='outline' onClick={() => window.location.reload()}>
            Yeniden dene
          </Button>
          <Button render={<Link to='/' />}>Ana sayfaya dön</Button>
        </div>
        {import.meta.env.DEV && error instanceof Error && error.stack && (
          <pre className='bg-muted text-muted-foreground max-h-64 overflow-auto rounded-md p-3 text-left text-xs'>{error.stack}</pre>
        )}
      </div>
    </div>
  );
}
