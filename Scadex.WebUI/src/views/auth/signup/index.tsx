import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { signUp } from '@/api/auth';
import { handleFormApiError } from '@/lib/axios-helper';
import { signUpRequestSchema, type SignUpRequest } from '@/models/auth';

export default function Signup() {
  const navigate = useNavigate();

  const form = useForm<SignUpRequest>({
    resolver: zodResolver(signUpRequestSchema),
    defaultValues: { userName: '', email: '', fullName: '', companyId: '', phoneNumber: '', password: '', passwordConfirm: '' }
  });

  const mutation = useMutation({
    // Kayit oturum ACMAZ: SignUpResponse kullanici/izin tasimadigi ve ayri bir
    // profil ucu olmadigi icin buradan tam bir oturum kurulamaz. Kullanici adi
    // giris formuna tasinir ki kullanici yeniden yazmasin.
    mutationFn: signUp,
    onSuccess: (_response, values) => {
      toast.success('Kayit tamamlandi. Simdi giris yapabilirsiniz.');
      navigate('/login', { replace: true, state: { userName: values.userName } });
    },
    onError: error => handleFormApiError(error, form.setError)
  });

  const errors = form.formState.errors;

  return (
    <div className='flex min-h-screen items-center justify-center p-6'>
      <div className='w-full max-w-sm'>
        <div className='mb-8 space-y-2 text-center'>
          <h1 className='text-2xl font-semibold tracking-tight'>Hesap oluştur</h1>
          <p className='text-sm text-muted-foreground'>Scadex'e kayıt olun</p>
        </div>

        <form onSubmit={form.handleSubmit(values => mutation.mutate(values))} noValidate>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor='fullName'>Ad soyad</FieldLabel>
              <Input id='fullName' autoFocus {...form.register('fullName')} />
              {errors.fullName && <FieldError>{errors.fullName.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='userName'>Kullanici adi</FieldLabel>
              <Input id='userName' autoComplete='username' {...form.register('userName')} />
              {errors.userName && <FieldError>{errors.userName.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='email'>E-posta</FieldLabel>
              <Input id='email' type='email' autoComplete='email' {...form.register('email')} />
              {errors.email && <FieldError>{errors.email.message}</FieldError>}
            </Field>

            <Field>
              {/* Faz 6'da firma secim listesine donusecek; simdilik acik alan. */}
              <FieldLabel htmlFor='companyId'>Firma ID</FieldLabel>
              <Input id='companyId' placeholder='00000000-0000-0000-0000-000000000000' {...form.register('companyId')} />
              {errors.companyId && <FieldError>{errors.companyId.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='password'>Parola</FieldLabel>
              <Input id='password' type='password' autoComplete='new-password' {...form.register('password')} />
              {errors.password && <FieldError>{errors.password.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='passwordConfirm'>Parola tekrar</FieldLabel>
              <Input id='passwordConfirm' type='password' autoComplete='new-password' {...form.register('passwordConfirm')} />
              {errors.passwordConfirm && <FieldError>{errors.passwordConfirm.message}</FieldError>}
            </Field>

            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kayit olunuyor...' : 'Kayit ol'}
            </Button>
          </FieldGroup>
        </form>

        <p className='mt-6 text-center text-sm text-muted-foreground'>
          Zaten hesabiniz var mi?{' '}
          <Link to='/login' className='font-medium text-foreground underline underline-offset-4'>
            Giris yapin
          </Link>
        </p>
      </div>
    </div>
  );
}
