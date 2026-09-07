// System share sheet for NativeShare.Share on iOS.
#import <UIKit/UIKit.h>

extern "C" UIViewController* UnityGetGLViewController();

extern "C" void _ftShare(const char* text)
{
    NSString *payload = [NSString stringWithUTF8String: text ? text : ""];
    if (payload.length == 0) return;

    UIViewController *root = UnityGetGLViewController();
    if (root == nil) return;

    UIActivityViewController *sheet =
        [[UIActivityViewController alloc] initWithActivityItems: @[payload]
                                         applicationActivities: nil];

    // On iPad the sheet is a popover, and presenting one without an anchor raises an
    // exception instead of showing anything. Anchor it to the middle of the screen.
    UIPopoverPresentationController *popover = sheet.popoverPresentationController;
    if (popover != nil)
    {
        popover.sourceView = root.view;
        popover.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds),
                                        CGRectGetMidY(root.view.bounds), 0, 0);
        popover.permittedArrowDirections = 0;
    }

    [root presentViewController: sheet animated: YES completion: nil];
}
