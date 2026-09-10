import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export interface NodePosition {
  x: number;
  y: number;
}

/**
 * Canvas'ta sol tuşun ne yaptığı.
 *
 * `connect` diye üçüncü bir mod YOK: kablo çizmek her zaman bir pin handle'ından
 * başlar, yani hedefi zaten kendisi belirtiyor — ayrı bir mod, kullanıcıdan
 * gereksiz bir hazırlık adımı istemek olurdu.
 */
export type DiagramMode = 'select' | 'pan';

/**
 * Diyagram editorunun ISTEMCI durumu. Sunucudan gelen graf verisi (device / pin /
 * connection) buraya girmez - o TanStack Query cache'inde durur. Burada yalnizca
 * secim, arac modu ve kaydedilmemis degisiklik bayragi tutulur.
 */
interface StateUI {
  selectedNodeIds: string[];
  selectedEdgeIds: string[];
  mode: DiagramMode;
  isDirty: boolean;
}

const initialState: StateUI = {
  selectedNodeIds: [],
  selectedEdgeIds: [],
  mode: 'select',
  isDirty: false
};

export const diagramSlice = createSlice({
  name: 'diagramSlice',
  initialState,
  reducers: {
    setSelection: (state, action: PayloadAction<{ nodeIds: string[]; edgeIds: string[] }>) => {
      state.selectedNodeIds = action.payload.nodeIds;
      state.selectedEdgeIds = action.payload.edgeIds;
    },
    clearSelection: state => {
      state.selectedNodeIds = [];
      state.selectedEdgeIds = [];
    },
    setMode: (state, action: PayloadAction<DiagramMode>) => {
      state.mode = action.payload;
    },
    setDirty: (state, action: PayloadAction<boolean>) => {
      state.isDirty = action.payload;
    }
  }
});

export const { setSelection, clearSelection, setMode, setDirty } = diagramSlice.actions;
export default diagramSlice.reducer;
