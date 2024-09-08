# License Options

VRCFury (c) 2024 Senky

For the purposes of this document, a "commercial purpose" is one primarily intended for commercial advantage or monetary compensation (including, but not limited to, one-time payments, subscription payments, and donations). A "personal purpose" is any purpose aside from those defined as a "commercial purpose."

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

You may choose to use any of the following licenses:

### Personal License

VRCFury may be used, copied, modified, merged, published, distributed, or sublicensed for personal purposes if all of the following restrictions are met:
* This full copyright and license notice is included in any derivitive works or distributions.

### Commercial License

VRCFury may be used for personal or commercial purposes if all of the following restrictions are met:
* VRCFury must be downloaded directly by the end-user from an archive distributed on https://vcc.vrcfury.com
* VRCFury must not be redistributed with your product
* The package may be downloaded by an interactive guided process and extracted from a compressed archive, but the source files must be left unmodified.

### VRCA License

VRCFury may distributed for personal or commercial purposes if all of the following restrictions are met:
* The distribution is made as part of an "uploaded avatar asset bundle," hosted on VRChat asset servers.

# FAQ
(These FAQ are for reference only and are not a part of the license above)

### Why isn't VRCFury fully "open source"?

For a project to officially be "FOSS" (Free and Open Source Software), its use must be unrestricted and unencumbered,
essentially meaning that it can be used by any person or company for any purpose (sometimes with limited restrictions).

We are not using a FOSS license because:

1. We want to prevent malicious avatar creators from modifying VRCFury and introducing it as an "exclusive" paid part of their avatar
   products. Not only would this be... evil, it also would cause these versions of VRCFury to diverge, preventing components
   made using one version from working on the other version.
2. We want to prevent corporations from taking VRCFury and making it a part of their game without asking. VRCFury includes
   a lot of novel unity logic which would be valuable to a VR or Desktop game company, and if they "took" all of our work
   and made it a built-in part of their game, the development priorities likely would shift toward monetization (as most
   companies do), rather than what's best for the users of VRCFury.


### Can I use VRCFury for an avatar that I sell?

Yes! As long as you do not distribute VRCFury itself along with your avatar package, you are still totally in
compliance with the license. Simply instruct your users to download VRCFury from VRCFury website.

### Why not use GPL / AGPL?

GPL can discourage commercial use by forcing commercial users to open-source their own applications using such a library.
However, this adds a lot of complexity in our case, as neither Unity nor the VRCSDK are GPL-compatible. Even with special exceptions for these,
we also need to allow custom non-GPL avatar scripts to interact with the VRCFury API, as well as other integrations. Without a way to give separate
permissions specifically for commercial use, there's not a good way to allow these exceptions while still achieving our
goals above.
