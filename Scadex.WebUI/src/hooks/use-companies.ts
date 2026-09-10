import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createCompany, getCompanyList, updateCompany } from '@/api/company';
import { cabinetKeys, companyKeys } from '@/api/query-keys';
import type { CompanyCreateRequest, CompanyUpdateRequest } from '@/models/company';

/** Firma kartları — pasifler DAHİL (geri alınabilmeleri için). */
export function useCompanies() {
  return useQuery({
    queryKey: companyKeys.list(),
    queryFn: getCompanyList
  });
}

/**
 * Firma listesini tazeler.
 *
 * `invalidateQueries` burada güvenli: firma ekranı salt-okunur bir listedir,
 * diyagram editörünün aksine üzerine yazılabilecek yerel bir düzenleme yok.
 * Sunucu zaten gövdesiz 200 döndüğü için `setQueryData` ile yazacak veri de yok.
 *
 * `onError` BİLEREK yok: hata politikası `handleFormApiError` ile forma ait ve
 * `form.setError`'a ihtiyaç duyar, o da yalnızca çağıran tarafta vardır.
 */
export function useCreateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CompanyCreateRequest) => createCompany(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: companyKeys.all });
      toast.success('Firma oluşturuldu.');
    }
  });
}

export function useUpdateCompany() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CompanyUpdateRequest) => updateCompany(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: companyKeys.all });
      // Kabin kartları firma adını `companyName` alanında TAŞIYOR; tazelenmezse
      // liste eski adı göstermeye devam eder.
      void queryClient.invalidateQueries({ queryKey: cabinetKeys.all });
      toast.success('Firma güncellendi.');
    }
  });
}
