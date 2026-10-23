# User Agreement and Privacy Policy for CL (Clegendary Launcher)
Version: 1.1.0 | Effective Date: October 23, 2026

This document governs the terms of use for the software "CL (Clegendary Launcher)" (including CL:Cross and Windows Edition) and outlines the processing of technical data. By accepting this Agreement upon launching the software, you confirm your acceptance of these terms.

---

## PART I. TERMS OF USE AND OPEN SOURCE (EULA)

### 1. Project Status and Age Restrictions
1.1. CL is free, non-commercial, open-source software (Open Source) developed by an independent community of enthusiasts.
1.2. You confirm that you have reached the age of digital consent under the laws of your country (16 years in most EU countries, 14 years in Ukraine), or have obtained the permission of a parent or legal guardian to use this software.

### 2. Code Licensing (GNU GPLv3)
2.1. The logic and source code of the Launcher are licensed under the GNU General Public License v3.0 (GPLv3). You have the right to freely inspect, modify, and redistribute the source code.
2.2. The full text of the license is included with the program (LICENSE file), and the complete source code is publicly accessible at:
- **Source Code Repository:** [https://github.com/WER-CORE/CL-OpenSource](https://github.com/WER-CORE/CL-OpenSource)
2.3. Nothing in this document shall limit your rights granted under the GNU GPLv3 regarding the source code.

### 3. Trademarks, Branding, and Third-Party APIs (Trademark Policy)
3.1. Pursuant to Section 7 of the GNU GPLv3 ("Additional Terms"), the GPLv3 license does NOT apply to trademarks, brand names, or visual branding assets.
3.2. The names "CL", "Clegendary Launcher", original logos, icons, and banners represent the visual identity of the project authors.
3.3. **Fork Requirements (Derivative Works):**
If you create your own modification or fork of the launcher, you must:
- Change the program name and completely replace all CL branding graphics with your own to avoid misleading users.
- Use your own client identifiers (Client IDs / API Keys) for Microsoft OAuth and CurseForge Core API services. Using the original CL project keys in third-party distributions is prohibited.

### 4. Minecraft EULA Compliance and Mod Repositories
4.1. The Launcher is an independent third-party client and is NOT an official product of Mojang Studios or Microsoft Corporation. "Minecraft" is a registered trademark of Mojang Synergies AB.
4.2. The Launcher does NOT distribute any original or modified game files. All game archives and libraries are downloaded exclusively from official Mojang/Microsoft content distribution servers.
4.3. Authentication is handled via the official secure Microsoft Identity protocol (OAuth 2.0). We encourage users to use official licensed copies of the game.
4.4. Access to modifications is provided via public CurseForge and Modrinth APIs with full author attribution and compliance with platform restrictions against unauthorized re-hosting.

### 5. Disclaimer of Warranties and Limitation of Liability
5.1. The Launcher is provided "AS IS", without warranty of any kind, in accordance with Sections 15 and 16 of the GNU GPLv3 license.
5.2. Developers shall not be liable for issues resulting from incompatible third-party mods, account bans by game servers, or hardware malfunctions. Where applicable law does not permit full disclaimer of liability, liability is limited to the minimum extent allowed by law.

---

## PART II. PRIVACY AND DATA PROTECTION POLICY (GDPR)

### 6. Data Controller and Contact Information
6.1. The Data Controller within the meaning of Regulation (EU) 2016/679 (GDPR) and Ukrainian data protection legislation is the CL Open Source Community.
6.2. Official contacts for exercising data subject rights (inquiries, log deletion, consent withdrawal):
- **Official Email:** [clsup125@gmail.com](mailto:clsup125@gmail.com)
- **GitHub Issues:** [https://github.com/WER-CORE/CL-OpenSource/issues](https://github.com/WER-CORE/CL-OpenSource/issues)
- **Discord:** clegendary

### 7. Privacy by Design and Data Minimization
7.1. The Launcher does NOT collect passwords, personal files, browsing history, or sensitive personal data.
7.2. All Microsoft authentication tokens, launcher settings, and local profiles are stored strictly on your local device.

### 8. Legal Bases for Processing Technical Data
8.1. **Application Updates:**
- *Legal basis:* Article 6(1)(f) GDPR (Legitimate interest in maintaining software security and stability).
- *Data processed:* For update package delivery over HTTP/HTTPS, the web server temporarily records client IP addresses in standard web server logs (log rotation up to 30 days). No user profiling or tracking is conducted.
8.2. **Crash Reports (Sentry):**
- *Legal basis:* Article 6(1)(a) GDPR (Explicit voluntary consent — OPT-IN).
- *Terms:* Automatic crash reporting is **DISABLED by default**. It can only be activated by your explicit opt-in in the program settings.
- *Data sent:* .NET technical error stack trace, OS version, and launcher version. Data Scrubbing filters automatically sanitize local file paths to remove usernames (`sendDefaultPii = false`).
- *International Data Transfers:* Sentry (Functional Software, Inc., USA) processes technical error logs under the EU-U.S. Data Privacy Framework (DPF) and Standard Contractual Clauses (SCC). Data retention is up to 90 days.

### 9. Your Rights and Policy Changes
9.1. You have the right to withdraw your consent to crash reporting at any time by toggling off the option in the Launcher Settings.
9.2. In case of material changes to this document, users will be notified inside the application interface with the new document version. Prior revisions are archived in the project repository.
