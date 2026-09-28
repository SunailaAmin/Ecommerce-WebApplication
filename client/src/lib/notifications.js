import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { API_BASE, getToken } from './api';

const HUB_PATH = '/hubs/order-notifications';

/**
 * Keeps a SignalR connection open while the user is signed in and calls onNotification
 * for every order event addressed to them.
 */
export function useOrderNotifications(enabled, onNotification) {
  const connectionRef = useRef(null);
  const handlerRef = useRef(onNotification);

  handlerRef.current = onNotification;

  useEffect(() => {
    if (!enabled) {
      connectionRef.current?.stop();
      connectionRef.current = null;
      return undefined;
    }

    const token = getToken();
    if (!token) return undefined;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}${HUB_PATH}`, {
        accessTokenFactory: () => getToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('OrderNotification', (notification) => handlerRef.current?.(notification));

    connection
      .start()
      .then(() => {
        if (connection.state === HubConnectionState.Connected) {
          document.dispatchEvent(new CustomEvent('notifications:connected'));
        }
      })
      .catch(() => {
        // A failed live connection must not break the page; the order pages still
        // show the current status whenever they are opened.
      });

    connectionRef.current = connection;

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [enabled]);
}
