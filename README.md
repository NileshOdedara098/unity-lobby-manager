# Unity Lobby Manager Editor Tool
<img width="632" height="866" alt="image" src="https://github.com/user-attachments/assets/512cae68-0b7b-4f3e-aac6-ca6ad849998e" />

An editor window for managing Unity Lobby Service lobbies directly from the Unity Editor.

![Lobby Manager Interface](.github/screenshot.png) *(optional: add screenshot)*

## Features
- 🔑 Service account authentication
- 🔄 List/refresh public lobbies with pagination
- 👥 View players in lobbies
- 🗑️ Delete individual lobbies or mass delete
- 📊 Analytics tracking (player counts, deletions)
- 📥 Export analytics data to JSON

## Installation
1. Create `Assets/Editor/LobbyManager` folder
2. Add `LobbyManagerWindow.cs` to this folder
3. Install dependencies via Package Manager:
   - `Newtonsoft.Json` (required)

## Usage
1. Open window: **Window > Lobby Manager**
2. Enter your [Unity Service Account](https://dashboard.unity3d.com/service-accounts) credentials:
   - Key ID
   - Secret Key
   - Project ID
   - Environment ID (`production` or `staging`)
3. Click **Authenticate**
4. Use toolbar buttons:
   - **Refresh Lobbies**: Load first page
   - **Load More**: Paginate results
   - **View Players**: Inspect lobby occupants
   - **Delete**: Remove single lobby
   - **Force Delete All**: Remove all listed lobbies

## Security Warning
🔒 **Credentials are stored in plain text** (Unity's `EditorPrefs`). Never commit:
- `EditorPrefs` files (`UnityEditor-*.prefs`)
- Project files containing your credentials

## Analytics Data
- Stored in `Assets/LobbyAnalytics.json`
- Includes:
  - Event history (authentication, deletions)
  - Max players observed
  - Total deletions
  - Timestamps

## Dependencies
- [Unity Lobby Service](https://docs.unity.com/lobby)
- Newtonsoft.Json (via Package Manager)
- Unity 2021.3+

## Troubleshooting
- "Authentication failed": Verify service account permissions
- Empty lobby lists: Check environment ID matches lobby service
- HTTP errors: Ensure project ID matches lobby service config

## Contribution
Contributions welcome! Please submit PRs to:
- [ ] Add credential encryption
- [ ] Improve error handling
- [ ] Add lobby creation tools
