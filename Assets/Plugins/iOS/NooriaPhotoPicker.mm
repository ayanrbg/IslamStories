// Picks one photo for the avatar with the system PHPicker (iOS 14+): no Photos
// permission is needed, the app only gets the picture the parent chose. The image is
// redrawn upright, scaled down to 1600 px and written to the temp folder; Unity gets the
// path (or "" when cancelled) through UnitySendMessage. C# side: NativePhotoPicker.cs.

#import <UIKit/UIKit.h>
#import <PhotosUI/PhotosUI.h>

extern UIViewController* UnityGetGLViewController();
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

@interface NooriaPhotoPicker : NSObject <PHPickerViewControllerDelegate>
@property (nonatomic, copy) NSString* gameObject;
@property (nonatomic, copy) NSString* method;
@end

static NooriaPhotoPicker* s_picker = nil;

@implementation NooriaPhotoPicker

- (void)send:(NSString*)path
{
    UnitySendMessage(self.gameObject.UTF8String, self.method.UTF8String, (path ?: @"").UTF8String);
    s_picker = nil;
}

- (void)picker:(PHPickerViewController*)picker didFinishPicking:(NSArray<PHPickerResult*>*)results
{
    [picker dismissViewControllerAnimated:YES completion:nil];
    NSItemProvider* provider = results.firstObject.itemProvider;
    if (provider == nil || ![provider canLoadObjectOfClass:[UIImage class]]) { [self send:@""]; return; }

    [provider loadObjectOfClass:[UIImage class] completionHandler:^(id<NSItemProviderReading> object, NSError* error) {
        NSString* path = @"";
        UIImage* image = [object isKindOfClass:[UIImage class]] ? (UIImage*)object : nil;
        if (image != nil)
        {
            CGSize size = image.size;
            CGFloat k = MIN(1.0, 1600.0 / MAX(size.width, size.height));
            CGSize target = CGSizeMake(round(size.width * k), round(size.height * k));
            UIGraphicsImageRendererFormat* format = [UIGraphicsImageRendererFormat defaultFormat];
            format.scale = 1;
            UIGraphicsImageRenderer* renderer = [[UIGraphicsImageRenderer alloc] initWithSize:target format:format];
            // drawInRect applies the image orientation, so the file comes out upright.
            UIImage* upright = [renderer imageWithActions:^(UIGraphicsImageRendererContext* ctx) {
                [image drawInRect:CGRectMake(0, 0, target.width, target.height)];
            }];
            NSData* jpeg = UIImageJPEGRepresentation(upright, 0.9);
            NSString* file = [NSTemporaryDirectory() stringByAppendingPathComponent:@"avatar_pick.jpg"];
            if (jpeg != nil && [jpeg writeToFile:file atomically:YES]) path = file;
        }
        dispatch_async(dispatch_get_main_queue(), ^{ [self send:path]; });
    }];
}

@end

extern "C" void NooriaPickPhoto(const char* gameObject, const char* method)
{
    s_picker = [NooriaPhotoPicker new];
    s_picker.gameObject = [NSString stringWithUTF8String:gameObject];
    s_picker.method = [NSString stringWithUTF8String:method];

    PHPickerConfiguration* config = [[PHPickerConfiguration alloc] init];
    config.filter = [PHPickerFilter imagesFilter];
    config.selectionLimit = 1;
    PHPickerViewController* picker = [[PHPickerViewController alloc] initWithConfiguration:config];
    picker.delegate = s_picker;
    [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
}
