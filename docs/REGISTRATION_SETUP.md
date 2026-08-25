# Registration Tablet ↔ Game (TCP)

Horse Racing acts as the **TCP server** (same pattern as AlkameenCarSim / BleachMultiball). The **Registration** tablet app is the **client**.

## Quick setup

1. Open `Main.unity` in Horse Racing.
2. Run menu: **Horse Racing → Setup Registration TCP Bridge**
3. Build & run Horse Racing on the **game PC** (server).
4. Run **Registration** app on the **tablet** (client).

## Network defaults

| Setting | Value |
|---------|-------|
| Protocol | TCP |
| Game port | **1234** |
| Discovery UDP | **3738** (broadcast) |
| Message format | 4-byte little-endian length + UTF-8 JSON |

## Registration tablet config

In Registration app Tab settings (hidden panel):

- **ServiceMode**: Enable  
- **ServiceType**: Client  
- **GetServerIP**: Automatic (listens on UDP 3738) or Manual (game PC LAN IP)  
- **Protocol**: TCP  
- **Port**: 1234  

## Flow

1. Game starts → sends `restart: true` to connected clients.
2. Tablet registers player 1 & 2 → game receives `entries[]`, sets HUD names, shows **Instructions**.
3. Tablet presses Start → game receives `start: true` → countdown + race.
4. Tablet restart/end → game resets to start page.

## CSV log

Registrations append to:

`%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/RegisteredUser.txt`

## Energizing Panel Speedometer

Same bridge scripts were added to `EnergizingPanel_Speedometer`:

- Menu: **Energizing Panel → Setup Registration TCP Bridge**
- Only one game should listen on port 1234 at a time.

## Troubleshooting

- Enable **Log Traffic** on `RegistrationTcpServer` in Inspector.
- Windows Firewall: allow inbound TCP 1234 on game PC.
- If discovery fails, set tablet IP manually to the game machine's LAN address.
