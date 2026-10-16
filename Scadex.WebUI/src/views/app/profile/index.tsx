import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { handleFormApiError } from '@/lib/axios-helper';
import { getSession, useRoles } from '@/lib/auth-session';
import { useChangeOwnPassword, useProfile, useUpdateProfile } from '@/hooks/use-profile';
import {
  userOwnPasswordChangeSchema,
  userProfileUpdateRequestSchema,
  type UserOwnPasswordChangeForm,
  type UserProfileDto,
  type UserProfileUpdateRequest
} from '@/models/user';

/**
 * Profilim — `/profile`. Her giriş yapan kullanıcıya açıktır, izin istemez; sunucu kimliği token'dan okur.
 *
 * Kullanıcı yalnızca ad soyad, e-posta ve telefonunu yazar. Kullanıcı adı, firma, kart ve roller burada salt okunurdur:
 * onları yönetici (Kullanıcılar ekranı / Kart tanımlama) değiştirir. Parola değişikliği mevcut parolayı sorar; yönetici
 * ekranındaki "Parola değiştir" sormaz.
 */
export default function Profile() {
  const query = useProfile();

  return (
    <div className='flex max-w-3xl flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Profilim</h1>
        <p className='text-sm text-muted-foreground'>Hesap bilgileriniz ve parolanız.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Hesap bilgileri</CardTitle>
          <CardDescription>Kullanıcı adı, firma, kart ve roller yönetici tarafından değiştirilir.</CardDescription>
        </CardHeader>
        <CardContent>
          {query.isPending && <Skeleton className='h-64 w-full rounded-lg' />}
          {query.isError && <p className='text-sm text-destructive'>{query.error.message}</p>}
          {/* Form ancak veri gelince mount edilir: varsayılan değerler veriden okunur, sonradan `reset` gerekmez. */}
          {query.data && <ProfileForm profile={query.data} />}
        </CardContent>
      </Card>

      <PasswordCard userName={query.data?.userName ?? null} />
    </div>
  );
}

// ─────────────────────────────────────────────────────────── profil

function toFormValues(profile: UserProfileDto): UserProfileUpdateRequest {
  return { fullName: profile.fullName, email: profile.email ?? '', phoneNumber: profile.phoneNumber ?? '' };
}

function ProfileForm({ profile }: { profile: UserProfileDto }) {
  const roles = useRoles();
  const form = useForm<UserProfileUpdateRequest>({
    resolver: zodResolver(userProfileUpdateRequestSchema),
    defaultValues: toFormValues(profile)
  });

  const mutation = useUpdateProfile();
  const errors = form.formState.errors;

  return (
    <form
      onSubmit={form.handleSubmit(values =>
        mutation.mutate(values, {
          // Sunucunun kırptığı/null'a çektiği değerler forma geri yazılır; `isDirty` sıfırlanır.
          onSuccess: updated => form.reset(toFormValues(updated)),
          onError: error => handleFormApiError(error, form.setError)
        })
      )}
      noValidate>
      <FieldGroup>
        <dl className='grid gap-x-4 gap-y-3 rounded-lg border p-3 text-sm sm:grid-cols-2'>
          <ReadOnlyItem label='Kullanıcı adı' value={profile.userName} />
          <ReadOnlyItem label='Firma' value={profile.companyName} />
          <ReadOnlyItem label='Kart numarası' value={profile.identityCardId} mono />
          <div className='flex flex-col gap-1'>
            <dt className='text-muted-foreground'>Roller</dt>
            <dd className='flex flex-wrap gap-1'>
              {roles.length === 0 ? '—' : roles.map(role => <Badge key={role} variant='outline'>{role}</Badge>)}
            </dd>
          </div>
        </dl>

        <Field>
          <FieldLabel htmlFor='profile-full-name'>Ad soyad</FieldLabel>
          <Input id='profile-full-name' autoComplete='name' {...form.register('fullName')} />
          {errors.fullName && <FieldError>{errors.fullName.message}</FieldError>}
        </Field>

        <div className='grid gap-4 sm:grid-cols-2'>
          <Field>
            <FieldLabel htmlFor='profile-email'>E-posta</FieldLabel>
            <Input id='profile-email' type='email' autoComplete='email' {...form.register('email')} />
            <FieldDescription>İsteğe bağlı; doluysa girişte kullanıcı adı yerine kullanılabilir.</FieldDescription>
            {errors.email && <FieldError>{errors.email.message}</FieldError>}
          </Field>

          <Field>
            <FieldLabel htmlFor='profile-phone'>Telefon</FieldLabel>
            <Input id='profile-phone' type='tel' autoComplete='tel' {...form.register('phoneNumber')} />
            {errors.phoneNumber && <FieldError>{errors.phoneNumber.message}</FieldError>}
          </Field>
        </div>
      </FieldGroup>

      <div className='mt-4 flex justify-end'>
        <Button type='submit' disabled={mutation.isPending || !form.formState.isDirty}>
          {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
        </Button>
      </div>
    </form>
  );
}

function ReadOnlyItem({ label, value, mono }: { label: string; value: string | null; mono?: boolean }) {
  return (
    <div className='flex flex-col gap-1'>
      <dt className='text-muted-foreground'>{label}</dt>
      <dd className={mono && value ? 'font-mono text-xs' : undefined}>{value || '—'}</dd>
    </div>
  );
}

// ─────────────────────────────────────────────────────────── parola

/** Mevcut parola sorulur. Bu cihazın oturumu korunur, diğer cihazlardaki oturumlar yeniden giriş ister. */
function PasswordCard({ userName }: { userName: string | null }) {
  const form = useForm<UserOwnPasswordChangeForm>({
    resolver: zodResolver(userOwnPasswordChangeSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' }
  });

  const mutation = useChangeOwnPassword();
  const errors = form.formState.errors;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Parola değiştir</CardTitle>
        <CardDescription>Yeni parola hemen geçerli olur. Bu cihazda oturumunuz açık kalır; diğer cihazlarda yeniden giriş yapmanız gerekir.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          onSubmit={form.handleSubmit(values =>
            mutation.mutate(
              { currentPassword: values.currentPassword, newPassword: values.newPassword, deviceId: getSession()?.deviceId ?? null },
              { onSuccess: () => form.reset(), onError: error => handleFormApiError(error, form.setError) }
            )
          )}
          noValidate>
          {/* Parola yöneticileri hangi hesabın parolası olduğunu bu gizli alandan anlar. */}
          {userName && <input type='text' name='username' autoComplete='username' value={userName} readOnly hidden />}

          <FieldGroup>
            <Field>
              <FieldLabel htmlFor='profile-current-password'>Mevcut parola</FieldLabel>
              <Input id='profile-current-password' type='password' autoComplete='current-password' {...form.register('currentPassword')} />
              {errors.currentPassword && <FieldError>{errors.currentPassword.message}</FieldError>}
            </Field>

            <div className='grid gap-4 sm:grid-cols-2'>
              <Field>
                <FieldLabel htmlFor='profile-new-password'>Yeni parola</FieldLabel>
                <Input id='profile-new-password' type='password' autoComplete='new-password' {...form.register('newPassword')} />
                {errors.newPassword && <FieldError>{errors.newPassword.message}</FieldError>}
              </Field>

              <Field>
                <FieldLabel htmlFor='profile-confirm-password'>Yeni parola (tekrar)</FieldLabel>
                <Input id='profile-confirm-password' type='password' autoComplete='new-password' {...form.register('confirmPassword')} />
                {errors.confirmPassword && <FieldError>{errors.confirmPassword.message}</FieldError>}
              </Field>
            </div>
          </FieldGroup>

          <div className='mt-4 flex justify-end'>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : 'Parolayı değiştir'}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
