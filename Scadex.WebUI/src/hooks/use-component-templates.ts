import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createComponentTemplate, getPalette, uploadTemplateImage } from '@/api/component-template';
import { componentTemplateKeys } from '@/api/query-keys';
import { toApiError } from '@/lib/axios-helper';
import type { ComponentTemplateCreateRequest } from '@/models/componentTemplate';

/** Uzun `staleTime`: palet her kabinette aynıdır ve yalnızca şablon yazarlığı değiştirir. */
export function useComponentTemplatePalette() {
  return useQuery({
    queryKey: componentTemplateKeys.palette(),
    queryFn: getPalette,
    staleTime: 30 * 60 * 1000
  });
}

/**
 * Şablon arka plan görselini yükler.
 *
 * Cache'e DOKUNMAZ: yükleme henüz hiçbir şablona bağlı değil, yalnızca diske bir
 * dosya koyup URL'sini döndürüyor. Şablon kaydedilmezse dosya öksüz kalır —
 * bunun bedeli birkaç KB, alternatifi ise kullanıcının görseli görmeden pin
 * koyması olurdu.
 */
export function useUploadTemplateImage() {
  return useMutation({
    mutationFn: (file: File) => uploadTemplateImage(file),
    onError: error => {
      toast.error(toApiError(error).message);
    }
  });
}

/**
 * Yeni palet şablonu yazar (şablon + pinler tek transaction).
 *
 * Sözleşme: `docs/api-contract/10-component-template.md`
 *
 * Başarıda palet **invalidate** edilir — `setQueryData` DEĞİL. Canvas ayarlarının
 * aksine sunucu burada yalnızca `{ id }` dönüyor; palet kartının ihtiyaç duyduğu
 * `deviceTypeId`, renk ve pin şeması (pinlerin Id'leri dahil) sunucudan gelmediği
 * için cache'i yerinde güncellemek uydurma veri yazmak olurdu.
 *
 * Bu invalidation güvenli: palet ayrı bir anahtarda duruyor ve diyagram grafına
 * dokunmuyor, yani kaydedilmemiş bir düzenlemeyi ezme riski yok.
 */
export function useCreateTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ComponentTemplateCreateRequest) => createComponentTemplate(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: componentTemplateKeys.palette() });
      toast.success('Şablon oluşturuldu.');
    },
    onError: error => {
      // Alan bazlı hataları form zaten zod ile önden yakalıyor; buraya düşen
      // şey sunucunun söylediği ve istemcinin bilemeyeceği bir sebep.
      toast.error(toApiError(error).message);
    }
  });
}
