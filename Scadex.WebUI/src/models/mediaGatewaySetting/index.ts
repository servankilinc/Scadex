// Queries — sunucu çıktısı, saf interface
export type { MediaGatewaySettingDto } from './queries/mediaGatewaySettingDto';

// Commands — şema + sunucu şekli
export {
  RTSP_TRANSPORTS,
  mediaGatewaySettingFormSchema,
  toRtspTransport,
  type MediaGatewaySettingFormValues,
  type MediaGatewaySettingUpdateRequest,
  type RtspTransport
} from './commands/mediaGatewaySettingForm';
