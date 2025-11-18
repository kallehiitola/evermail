# Bug Fix Verification: JwtTokenService RefreshToken GUID Parsing

## Bug Description

**Location**: `JwtTokenService.cs` line 155

**Issue**: The code attempted to parse a hashed refresh token string as a GUID, which would always fail at runtime.

### Old Code (BUGGY):

```csharp
// Line 155 - BEFORE
storedToken.ReplacedByTokenId = Guid.Parse(HashToken(newTokenPair.RefreshToken));
```

### Why It Failed:

1. `newTokenPair.RefreshToken` is a Base64-encoded random string (e.g., `"j5xK2pQ7r8/A..."`)
2. `HashToken()` returns a Base64-encoded SHA256 hash (e.g., `"abc123xyz+/="`)
3. `Guid.Parse()` expects format like `"00000000-0000-0000-0000-000000000000"`
4. **Result**: `FormatException` thrown at runtime

### Example Failure:

```csharp
// What was happening:
var refreshToken = "j5xK2pQ7r8/A1Bm3CnD4Ep5Fq6Gr7Hs8It9Ju0Kv1Lw2Mx3Ny4Oz5P..."; // Base64
var hash = HashToken(refreshToken); // "abc123xyz+/=" (Base64 SHA256 hash)
var guid = Guid.Parse(hash); // ❌ THROWS FormatException!
```

## Fix Applied

### Changes Made:

#### 1. Updated `TokenPair` Record to Include RefreshTokenId

**File**: `IJwtTokenService.cs`

```csharp
// BEFORE
public record TokenPair(
    string AccessToken, 
    string RefreshToken, 
    DateTime AccessTokenExpires, 
    DateTime RefreshTokenExpires
);

// AFTER
public record TokenPair(
    string AccessToken, 
    string RefreshToken, 
    Guid RefreshTokenId,  // ✅ Added
    DateTime AccessTokenExpires, 
    DateTime RefreshTokenExpires
);
```

#### 2. Return RefreshToken.Id in GenerateTokenPairAsync

**File**: `JwtTokenService.cs` lines 116-122

```csharp
// BEFORE
return new TokenPair(
    accessToken, 
    refreshTokenString, 
    DateTime.UtcNow.AddMinutes(15),
    refreshToken.ExpiresAt
);

// AFTER
return new TokenPair(
    accessToken, 
    refreshTokenString,
    refreshToken.Id,  // ✅ Include the actual GUID from database
    DateTime.UtcNow.AddMinutes(15),
    refreshToken.ExpiresAt
);
```

#### 3. Use RefreshTokenId Instead of Parsing Hash

**File**: `JwtTokenService.cs` line 156

```csharp
// BEFORE (BUGGY)
storedToken.ReplacedByTokenId = Guid.Parse(HashToken(newTokenPair.RefreshToken));

// AFTER (FIXED)
storedToken.ReplacedByTokenId = newTokenPair.RefreshTokenId;
```

## Why This Fix Works

1. **Type Safety**: `RefreshTokenId` is already a `Guid`, no parsing needed
2. **Correct Reference**: Points to the actual database ID of the new RefreshToken entity
3. **Audit Trail**: Maintains proper chain of token rotation for security auditing
4. **No Runtime Errors**: No string-to-GUID conversion that could fail

## Impact Assessment

### Affected Code:
- ✅ **No breaking changes** - All consuming code only uses existing properties
- ✅ **No linter errors** - Verified compilation succeeds
- ✅ **Compatible with existing usage** - Sites using `tokenPair.AccessToken` etc. unaffected

### Files Changed:
1. `Evermail.Infrastructure/Services/IJwtTokenService.cs` - Interface definition
2. `Evermail.Infrastructure/Services/JwtTokenService.cs` - Implementation

### Files Checked (No Changes Needed):
- `AuthEndpoints.cs` - Uses only `.AccessToken`, `.RefreshToken`, etc.
- `OAuthEndpoints.cs` - Uses only `.AccessToken`, `.RefreshToken`
- `AuthenticationStateService.cs` - Not affected

## Testing Recommendations

### Manual Test Scenario:

1. **Login** with username/password → Generates initial token pair
2. **Wait 13+ minutes** → Access token near expiration
3. **Call `/api/auth/refresh`** with refresh token
4. **Expected**: New token pair returned, old token revoked with proper `ReplacedByTokenId` link

### Before Fix:
```
❌ FormatException: "Guid should contain 32 digits with 4 dashes"
```

### After Fix:
```
✅ Token refresh succeeds
✅ ReplacedByTokenId = <GUID of new RefreshToken>
✅ Audit trail intact
```

## Conclusion

**Status**: ✅ **FIXED**

The bug was confirmed and fixed. The code now correctly stores the database ID of the new RefreshToken entity instead of attempting to parse a hash string as a GUID.

**Credit**: Bug identified by AI code review  
**Fixed**: 2025-11-14  
**Verified**: Compilation successful, no breaking changes

