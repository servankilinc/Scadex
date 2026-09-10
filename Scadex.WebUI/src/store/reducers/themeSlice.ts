import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export type Theme = 'dark' | 'light' | 'system';

interface StateUI {
  activeTheme: Theme;
  storageKey: string;
  initialLoading: boolean;
}

const initialState: StateUI = {
  activeTheme: (localStorage.getItem('generator-theme-storage-key') as Theme) || 'system',
  storageKey: 'generator-theme-storage-key',
  initialLoading: false
};

export const themeSlice = createSlice({
  name: 'themeSlice',
  initialState,
  reducers: {
    setTheme: (state, action: PayloadAction<Theme>) => {
      state.activeTheme = action.payload;
      localStorage.setItem(state.storageKey, action.payload);
    },
    toggleTheme: state => {
      state.activeTheme = state.activeTheme === 'light' ? 'dark' : 'light';
      localStorage.setItem(state.storageKey, state.activeTheme);
    },
    setInitialLoading: (state, action: PayloadAction<boolean>) => {
      state.initialLoading = action.payload;
    },
  }
});

export const { setTheme, toggleTheme, setInitialLoading } = themeSlice.actions;

export default themeSlice.reducer;
