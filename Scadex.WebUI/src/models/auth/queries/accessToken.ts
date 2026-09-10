/** Ayna: Scadex.Core/Utils/Auth/AccessToken.cs */
export interface AccessToken {
  token: string;
  /** ISO-8601 UTC. Backend DateTime doner. */
  expiration: string;
}
