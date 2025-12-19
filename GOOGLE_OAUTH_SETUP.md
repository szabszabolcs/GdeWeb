# Google OAuth Beállítás - Gyors Útmutató

## ⚠️ HIBA: "invalid_client" (401)

Ez a hiba azt jelenti, hogy a Google OAuth Client ID vagy Client Secret nincs helyesen beállítva.

## 🔧 Megoldás lépései:

### 1. Google Cloud Console beállítások

1. **Látogasson a Google Cloud Console-ba:**
   - URL: https://console.cloud.google.com/
   - Jelentkezzen be Google fiókjával

2. **Projekt létrehozása vagy kiválasztása:**
   - Válasszon egy projektet vagy hozzon létre újat

3. **OAuth Consent Screen beállítása:**
   - Navigáljon: **APIs & Services** > **OAuth consent screen**
   - Válassza ki a **User Type**-ot (External vagy Internal)
   - Töltse ki a kötelező mezőket:
     - **App name**: GdeWeb
     - **User support email**: saját email
     - **Developer contact information**: saját email
   - Kattintson a **Save and Continue** gombra
   - Scopes: Alapértelmezett (openid, email, profile) elég
   - Test users: Ha External típust választott, adjon hozzá teszt felhasználókat

4. **OAuth 2.0 Client ID létrehozása:**
   - Navigáljon: **APIs & Services** > **Credentials**
   - Kattintson a **+ CREATE CREDENTIALS** gombra
   - Válassza az **OAuth client ID** opciót
   - Application type: **Web application**
   - Name: **GdeWeb OAuth Client**
   - **Authorized JavaScript origins**: 
     ```
     https://localhost:7046
     ```
   - **Authorized redirect URIs**: 
     ```
     https://localhost:7046/api/Auth/GoogleCallback
     ```
   - Kattintson a **CREATE** gombra

5. **Credentials másolása:**
   - Másolja ki a **Client ID** értéket (pl: `123456789-abcdefghijklmnop.apps.googleusercontent.com`)
   - Másolja ki a **Client Secret** értéket (pl: `GOCSPX-abcdefghijklmnopqrstuvwxyz`)
   - **FONTOS**: A Client Secret csak egyszer jelenik meg!

### 2. Konfiguráció beillesztése

#### GdeWebAPI/appsettings.json

Cserélje ki a placeholder értékeket:

```json
{
  "GoogleOAuth": {
    "ClientId": "123456789-abcdefghijklmnop.apps.googleusercontent.com",
    "ClientSecret": "GOCSPX-abcdefghijklmnopqrstuvwxyz",
    "RedirectUri": "https://localhost:7046/api/Auth/GoogleCallback"
  }
}
```

#### GdeWeb/appsettings.json

```json
{
  "GoogleOAuth": {
    "ClientId": "123456789-abcdefghijklmnop.apps.googleusercontent.com"
  }
}
```

### 3. Alkalmazás újraindítása

- Indítsa újra a **GdeWebAPI** projektet
- Indítsa újra a **GdeWeb** projektet

### 4. Tesztelés

1. Nyissa meg a bejelentkezési oldalt: `https://localhost:7294/signin`
2. Kattintson a **"Bejelentkezés Google-lal"** gombra
3. Jelentkezzen be Google fiókjával
4. Engedélyezze a hozzáférést

## ✅ Ellenőrzési lista

- [ ] Google Cloud Console projekt létrehozva
- [ ] OAuth Consent Screen beállítva
- [ ] OAuth 2.0 Client ID létrehozva
- [ ] Authorized redirect URI beállítva: `https://localhost:7046/api/Auth/GoogleCallback`
- [ ] Client ID beillesztve az `appsettings.json` fájlokba
- [ ] Client Secret beillesztve a `GdeWebAPI/appsettings.json` fájlba
- [ ] Alkalmazás újraindítva

## 🔍 További hibák és megoldások

### "redirect_uri_mismatch"
- **Ok**: A redirect URI nem egyezik meg a Google Cloud Console-ban beállítottal
- **Megoldás**: Ellenőrizze, hogy a redirect URI pontosan egyezik-e mindkét helyen

### "access_denied"
- **Ok**: A felhasználó nem adott engedélyt
- **Megoldás**: Normális eset, a felhasználó megtagadhatja

### "invalid_grant"
- **Ok**: Az authorization code lejárt vagy már felhasználták
- **Megoldás**: Próbálja újra a bejelentkezést

## 📝 Fontos megjegyzések

- A **Client Secret** soha ne kerüljön verziókezelésbe (git)!
- Production környezetben használjon production URL-eket
- A redirect URI-nek **pontosan** egyeznie kell a Google Cloud Console-ban beállítottal

