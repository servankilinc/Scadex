/** Ayna: Scadex.Model/Dtos/CanvasSettings/Commands/CanvasSettingsUpsertDto.cs — sözleşme: docs/api-contract/13-canvas-settings.md */
import { z } from 'zod';
import { BackgroundVariant } from '@/models/enums';

export const canvasSettingsUpsertSchema = z
  .object({
    gridSize: z.number().int().min(1, 'Grid boyutu en az 1').max(500, 'Grid boyutu en fazla 500'),
    snapToGrid: z.boolean(),
    backgroundVariant: z.union([
      z.literal(BackgroundVariant.None),
      z.literal(BackgroundVariant.Dots),
      z.literal(BackgroundVariant.Lines),
      z.literal(BackgroundVariant.Cross)
    ]),
    gridColor: z.string().trim().min(1, 'Grid rengi zorunlu').max(32),
    backgroundColor: z.string().trim().min(1, 'Arka plan rengi zorunlu').max(32),
    // minZoom = 0 React Flow'da sonsuz uzaklaşmaya izin verir ve canvas kaybolur.
    minZoom: z.number().min(0.05, 'En küçük yakınlaştırma en az 0.05').max(1, 'En küçük yakınlaştırma en fazla 1'),
    maxZoom: z.number().min(1, 'En büyük yakınlaştırma en az 1').max(10, 'En büyük yakınlaştırma en fazla 10')
  })
  .refine(v => v.maxZoom > v.minZoom, {
    message: 'En büyük yakınlaştırma, en küçükten büyük olmalı',
    path: ['maxZoom']
  });

export type CanvasSettingsUpsertRequest = z.infer<typeof canvasSettingsUpsertSchema>;
