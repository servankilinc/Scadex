import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { CameraBrand, CameraBrandLabels } from '@/models/enums';
import type { CameraDraft, DeviceCreateRequest } from '@/models/diagram';
import { EnumSelect, Field } from './panel-fields';

const CAMERA_BRANDS: CameraBrand[] = Object.values(CameraBrand);

/**
 * Zorunlu alanı olan cihazın oluşturma penceresi — bugün yalnızca kamera: RTSP adresi IP'den kurulduğu için sunucu
 * IP'siz kamerayı oluşturmaz (ve IP kabin içinde tekildir). Diğer tipler paletten bırakılınca beklemeden oluşturulur.
 *
 * Kurallar sunucudaki `DeviceUpdateRequestValidator` + `CameraDraftValidator`'ın ELLE tutulan aynasıdır (IP ≤ 45,
 * model ≤ 64, kullanıcı/parola ≤ 128). Tekillik gibi kontroller sunucudadır; ret toast olarak görünür ve pencere açık kalır.
 */
export function CreateDeviceDialog({
  request,
  onSubmit,
  onCancel
}: {
  request: DeviceCreateRequest;
  onSubmit: (request: DeviceCreateRequest) => Promise<boolean>;
  onCancel: () => void;
}) {
  const [name, setName] = useState(request.name);
  const [ipAddress, setIpAddress] = useState(request.ipAddress ?? '');
  const [camera, setCamera] = useState<CameraDraft>(
    request.camera ?? { brand: CameraBrand.Hikvision, model: null, description: null, username: null, password: null }
  );
  const [submitting, setSubmitting] = useState(false);

  const canSubmit = name.trim() !== '' && ipAddress.trim() !== '' && !submitting;

  const submit = async () => {
    setSubmitting(true);
    await onSubmit({
      ...request,
      name: name.trim(),
      ipAddress: ipAddress.trim(),
      camera: { ...camera, model: camera.model?.trim() || null, username: camera.username?.trim() || null, password: camera.password || null }
    });
    setSubmitting(false);
  };

  return (
    <Dialog open onOpenChange={next => !next && onCancel()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Kamera ekle</DialogTitle>
          <DialogDescription>Kamera, canlı izleme adresi kurulabilsin diye IP adresiyle oluşturulur.</DialogDescription>
        </DialogHeader>

        <form
          className='flex flex-col gap-3'
          onSubmit={event => {
            event.preventDefault();
            if (canSubmit) void submit();
          }}>
          <Field label='Ad' htmlFor='create-camera-name'>
            <Input id='create-camera-name' className='h-8' maxLength={128} value={name} onChange={e => setName(e.target.value)} />
          </Field>
          <Field label='IP adresi' htmlFor='create-camera-ip' hint='Zorunlu; bu kabinde başka bir kamerada olmamalı.'>
            <Input id='create-camera-ip' className='h-8' maxLength={45} autoFocus value={ipAddress} onChange={e => setIpAddress(e.target.value)} />
          </Field>
          <Field label='Marka'>
            <EnumSelect value={camera.brand} options={CAMERA_BRANDS} labels={CameraBrandLabels} onChange={brand => setCamera({ ...camera, brand })} />
          </Field>
          <Field label='Model' htmlFor='create-camera-model'>
            <Input id='create-camera-model' className='h-8' maxLength={64} value={camera.model ?? ''} onChange={e => setCamera({ ...camera, model: e.target.value })} />
          </Field>
          <div className='grid grid-cols-2 gap-2'>
            <Field label='Kullanıcı adı' htmlFor='create-camera-username'>
              <Input
                id='create-camera-username'
                className='h-8'
                maxLength={128}
                value={camera.username ?? ''}
                onChange={e => setCamera({ ...camera, username: e.target.value })}
              />
            </Field>
            <Field label='Parola' htmlFor='create-camera-password'>
              <Input
                id='create-camera-password'
                type='password'
                autoComplete='new-password'
                className='h-8'
                maxLength={128}
                value={camera.password ?? ''}
                onChange={e => setCamera({ ...camera, password: e.target.value })}
              />
            </Field>
          </div>

          <DialogFooter>
            <DialogClose render={<Button type='button' variant='outline' size='sm' />}>Vazgeç</DialogClose>
            <Button type='submit' size='sm' disabled={!canSubmit}>
              {submitting ? 'Ekleniyor…' : 'Ekle'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
