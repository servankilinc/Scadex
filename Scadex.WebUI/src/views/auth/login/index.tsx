import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useLocation, useNavigate, Link } from 'react-router';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { login } from '@/api/auth';
import { handleFormApiError } from '@/lib/axios-helper';
import { loginRequestSchema, type LoginRequest } from '@/models/auth';

export default function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  // from: korumali bir sayfadan geldiyse giristen sonra oraya donulur.
  // userName: kayittan geldiyse kullanici adini yeniden yazdirmamak icin.
  const navigationState = location.state as { from?: string; userName?: string } | null;
  const redirectTo = navigationState?.from ?? '/';

  const form = useForm<LoginRequest>({
    resolver: zodResolver(loginRequestSchema),
    defaultValues: { userName: navigationState?.userName ?? '', password: '' }
  });

  const mutation = useMutation({
    mutationFn: login,
    onSuccess: () => navigate(redirectTo, { replace: true }),
    onError: error => handleFormApiError(error, form.setError)
  });

  return (
    <div className='flex min-h-screen items-center justify-center p-6'>
      <div className='w-full max-w-sm'>
        <div className='mb-8 space-y-2 text-center'>
          <h1 className='text-2xl font-semibold tracking-tight'>Scadex</h1>
          <p className='text-sm text-muted-foreground'>Devam etmek icin giris yapin</p>
        </div>

        <form onSubmit={form.handleSubmit(values => mutation.mutate(values))} noValidate>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor='userName'>Kullanici adi</FieldLabel>
              <Input id='userName' autoComplete='username' autoFocus {...form.register('userName')} />
              {form.formState.errors.userName && <FieldError>{form.formState.errors.userName.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='password'>Parola</FieldLabel>
              <Input id='password' type='password' autoComplete='current-password' {...form.register('password')} />
              {form.formState.errors.password && <FieldError>{form.formState.errors.password.message}</FieldError>}
            </Field>

            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Giris yapiliyor...' : 'Giris yap'}
            </Button>
          </FieldGroup>
        </form>

        <p className='mt-6 text-center text-sm text-muted-foreground'>
          Hesabiniz yok mu?{' '}
          <Link to='/signup' className='font-medium text-foreground underline underline-offset-4'>
            Kayit olun
          </Link>
        </p>
      </div>
    </div>
  );
}
