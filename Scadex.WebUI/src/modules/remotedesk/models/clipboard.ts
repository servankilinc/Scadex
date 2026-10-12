/** Viewer hub pano işlemi sonucu (RemoteDesk.md § 12.9) — `ClipboardResult`'ın ELLE yazılmış TS aynası. İçe aktarmada `text` uzak panonun metni. */
export interface ClipboardResult {
  success: boolean;
  text: string | null;
  message: string | null;
}
