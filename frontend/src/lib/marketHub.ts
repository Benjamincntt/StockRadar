import * as signalR from "@microsoft/signalr";
import { getHubUrl } from "./hubUrl";

let connection: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;

export function getMarketHubConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(getHubUrl(), { withCredentials: true })
      .withAutomaticReconnect([0, 2000, 5000, 10_000, 30_000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }
  return connection;
}

export async function ensureMarketHubStarted(): Promise<signalR.HubConnection> {
  const conn = getMarketHubConnection();

  if (conn.state === signalR.HubConnectionState.Connected) {
    return conn;
  }

  if (conn.state === signalR.HubConnectionState.Connecting && startPromise) {
    await startPromise;
    return conn;
  }

  if (conn.state === signalR.HubConnectionState.Disconnected) {
    startPromise = conn.start().finally(() => {
      startPromise = null;
    });
    try {
      await startPromise;
    } catch {
      await new Promise((r) => setTimeout(r, 2000));
      if (conn.state === signalR.HubConnectionState.Disconnected) {
        await conn.start();
      }
    }
  }

  return conn;
}
