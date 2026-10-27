import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { AnnotationShape, AnnotationShapeLabels } from '@/models/enums';
import type { DiagramAnnotationItemDto } from '@/models/diagram';
import type { DiagramEditor } from '@/hooks/use-diagram-editor';
import { ANNOTATION_FONT_SIZE_MAX, ANNOTATION_FONT_SIZE_MIN, ANNOTATION_TEXT_MAX } from '@/lib/diagram/annotation-defaults';
import type { AnnotationNode } from '@/lib/diagram/to-rf-nodes';
import { DeleteButton } from './confirm-delete';
import { COORDINATE_LIMIT, ColorField, EnumSelect, Field, LayerControls, NumberInput, TextInput } from './panel-fields';

/**
 * Seçili notun paneli. Her alan commit'te ANINDA yazılır; editör dokunulan alana göre içerik
 * (`PUT …/annotations/{id}`) ya da yerleşim (`PUT …/layout`) ucunu seçer.
 */

const SHAPES: AnnotationShape[] = [AnnotationShape.Text, AnnotationShape.Rectangle, AnnotationShape.Note, AnnotationShape.Arrow];

export function AnnotationForm({ node, editor }: { node: AnnotationNode; editor: DiagramEditor }) {
  const { annotation } = node.data;
  const onChange = (patch: Partial<DiagramAnnotationItemDto>) => editor.updateAnnotation(node.id, patch);

  const isArrow = annotation.shape === AnnotationShape.Arrow;
  // `Text` şeklinde arka plan ve kenarlık RENDER'da yok sayılıyor; alanları
  // göstermek, değiştirdiğinde hiçbir şey olmayan bir kontrol sunmak olurdu.
  const hasBackground = annotation.shape === AnnotationShape.Rectangle || annotation.shape === AnnotationShape.Note;
  const hasBorder = hasBackground || isArrow;

  return (
    <div className='flex flex-col gap-3'>
      <Field label='Ad' htmlFor='annotation-name' hint='Şemada görünmez; listede tanımak için.'>
        {/* Boş ad sunucuda 400 döner — boş bırakılırsa eski değer korunuyor. */}
        <TextInput id='annotation-name' value={annotation.name} onCommit={value => value && onChange({ name: value })} />
      </Field>

      <Field label={isArrow ? 'Etiket' : 'Metin'} htmlFor='annotation-text' hint={isArrow ? 'Okun üstünde görünür. Boş bırakılabilir.' : undefined}>
        <Textarea
          id='annotation-text'
          className='min-h-16 text-xs'
          maxLength={ANNOTATION_TEXT_MAX}
          defaultValue={annotation.text}
          // Commit `blur`'de: her tuş vuruşunda yazmak, her harfi ayrı bir
          // istek yapardı (bkz. `TextInput`).
          onBlur={event => event.target.value !== annotation.text && onChange({ text: event.target.value })}
        />
      </Field>

      <Field label='Şekil'>
        <EnumSelect value={annotation.shape} options={SHAPES} labels={AnnotationShapeLabels} onChange={shape => onChange({ shape })} />
      </Field>

      <div className='grid grid-cols-2 gap-2'>
        <Field label='Genişlik' htmlFor='annotation-width' hint={isArrow ? 'Okun uzunluğu' : undefined}>
          {/* Sıfır genişlik/yükseklik: node canvas'ta görünmez olur ve kullanıcı
              onu bir daha seçip düzeltemez. Sunucu da bu yüzden reddediyor. */}
          <NumberInput id='annotation-width' value={annotation.width} min={1} max={COORDINATE_LIMIT} onCommit={width => onChange({ width })} />
        </Field>
        <Field label='Yükseklik' htmlFor='annotation-height' hint={isArrow ? 'Okun kalınlığı' : undefined}>
          <NumberInput id='annotation-height' value={annotation.height} min={1} max={COORDINATE_LIMIT} onCommit={height => onChange({ height })} />
        </Field>
      </div>

      <div className='grid grid-cols-2 gap-2'>
        <Field label='X' htmlFor='annotation-x'>
          <NumberInput
            id='annotation-x'
            value={Math.round(node.position.x)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={x => editor.moveNodes({ [node.id]: { x, y: node.position.y } })}
          />
        </Field>
        <Field label='Y' htmlFor='annotation-y'>
          <NumberInput
            id='annotation-y'
            value={Math.round(node.position.y)}
            min={-COORDINATE_LIMIT}
            max={COORDINATE_LIMIT}
            onCommit={y => editor.moveNodes({ [node.id]: { x: node.position.x, y } })}
          />
        </Field>
      </div>

      <Field label='Dönüş (°)' htmlFor='annotation-rotation' hint={isArrow ? 'Okun yönü.' : undefined}>
        <NumberInput id='annotation-rotation' value={annotation.rotation} min={0} max={359} onCommit={rotation => onChange({ rotation })} />
      </Field>

      <div className='flex flex-col gap-3 border-t pt-3'>
        <div className='grid grid-cols-2 gap-2'>
          <Field label='Yazı boyutu' htmlFor='annotation-font-size'>
            <NumberInput
              id='annotation-font-size'
              value={annotation.fontSize}
              min={ANNOTATION_FONT_SIZE_MIN}
              max={ANNOTATION_FONT_SIZE_MAX}
              onCommit={fontSize => onChange({ fontSize })}
            />
          </Field>
          <div className='flex items-end justify-between gap-2 pb-1'>
            <Label htmlFor='annotation-bold' className='text-xs'>
              Kalın
            </Label>
            <Switch id='annotation-bold' checked={annotation.isBold} onCheckedChange={isBold => onChange({ isBold })} />
          </div>
        </div>

        <ColorField label='Yazı rengi' id='annotation-font-color' value={annotation.fontColor} onCommit={fontColor => onChange({ fontColor })} />
        {hasBackground && (
          <ColorField
            label='Arka plan'
            id='annotation-bg-color'
            value={annotation.backgroundColor}
            onCommit={backgroundColor => onChange({ backgroundColor })}
          />
        )}
        {hasBorder && (
          <ColorField
            label={isArrow ? 'Ok rengi' : 'Kenarlık'}
            id='annotation-border-color'
            value={annotation.borderColor}
            onCommit={borderColor => onChange({ borderColor })}
          />
        )}
      </div>

      <LayerControls
        idPrefix='annotation'
        zIndex={annotation.zIndex}
        isLocked={annotation.isLocked}
        isVisible={annotation.isVisible}
        onChange={patch => onChange(patch)}
      />

      <div className='border-t pt-3'>
        <DeleteButton
          label='Notu sil'
          title='Notu sil'
          summary={
            <>
              <span className='font-medium'>{annotation.name}</span> silinecek ve geri alınamaz.
            </>
          }
          onConfirm={() => editor.deleteElements([node.id], [])}
        />
      </div>
    </div>
  );
}
