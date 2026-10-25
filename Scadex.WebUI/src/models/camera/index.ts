// Queries — sunucu çıktısı, saf interface
export type { CameraDto } from './queries/cameraDto';
export type { CameraCaptureDto } from './queries/cameraCaptureDto';
export type { StreamTokenDto } from './queries/streamTokenDto';

// Commands — C# DTO aynaları, saf interface
export type { CameraCaptureCreateRequest } from './commands/cameraCaptureCreateRequest';

// Form — düzenleme formunun şekli + sunucu şekline projeksiyon (oluşturma yalnızca diyagramda)
export {
  cameraFormSchema,
  emptyCameraForm,
  toCameraForm,
  toUpdateRequest,
  type CameraFormValues,
  type CameraUpdateRequest
} from './commands/cameraForm';
