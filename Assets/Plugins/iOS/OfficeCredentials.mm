#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static NSMutableDictionary *OfficeQuery(void) {
    return [@{(__bridge id)kSecClass:(__bridge id)kSecClassGenericPassword,
              (__bridge id)kSecAttrService:@"com.officeclash.credentials.v1",
              (__bridge id)kSecAttrAccount:@"playfab"} mutableCopy];
}

extern "C" int OfficeCredentialsSave(const char *value) {
    if (!value) return 0;
    @autoreleasepool {
        NSData *data = [NSData dataWithBytes:value length:strlen(value)];
        NSMutableDictionary *query = OfficeQuery();
        NSDictionary *update = @{(__bridge id)kSecValueData:data,
                                 (__bridge id)kSecAttrAccessible:(__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly};
        OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
        if (status == errSecItemNotFound) {
            [query addEntriesFromDictionary:update];
            status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
        }
        return status == errSecSuccess ? 1 : 0;
    }
}

// Returned strings are allocated with malloc and released by the caller.
extern "C" char *OfficeCredentialsLoad(void) {
    @autoreleasepool {
        NSMutableDictionary *query = OfficeQuery();
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef output = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &output);
        if (status == errSecItemNotFound) return NULL;
        if (status != errSecSuccess || output == NULL) return strdup("");
        NSData *data = CFBridgingRelease(output);
        NSString *value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return strdup(value ? [value UTF8String] : "");
    }
}

extern "C" int OfficeCredentialsClear(void) {
    OSStatus status = SecItemDelete((__bridge CFDictionaryRef)OfficeQuery());
    return status == errSecSuccess || status == errSecItemNotFound ? 1 : 0;
}

extern "C" void OfficeCredentialsFree(char *value) { free(value); }
