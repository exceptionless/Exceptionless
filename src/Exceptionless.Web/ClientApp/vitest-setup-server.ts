// Polyfill WebSocket for Node.js environment using mock-socket
// This is required for vitest-websocket-mock to work properly in server tests
import { WebSocket } from 'mock-socket';

globalThis.WebSocket = WebSocket as typeof globalThis.WebSocket;
