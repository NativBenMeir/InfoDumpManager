# Epic Name
GEM Ingestion, Summarization, and Smart Categorization

## 1. Goal
- **Problem:** Users collect scattered information (web pages, emails, blog posts, PDFs) without a unified place to summarize, organize, and later query it. Manual filing and categorization are time-consuming, inconsistent, and limit the ability to retrieve insights quickly.
- **Solution:** Provide a capture workflow that ingests sources (start with web pages), automatically creates a GEM (title, original link, saved copy, AI summary), and assigns or creates categories via AI. The system proposes tags to sub-divide GEMs within a category and connect related GEMs across categories. Users can adjust categories/tags and request on-demand category-level summaries or Q&A grounded in stored GEMs.
- **Impact:** Faster knowledge capture, improved findability, and higher confidence in stored knowledge. Expected lift in retained insights, reduced manual filing time, and higher reuse of saved information.

## 2. User Personas
- **Personal Knowledge Worker:** Saves articles and references for learning and personal projects.
- **Professional/Consultant:** Curates research, client materials, and competitive intel with reusable summaries.
- **Researcher/Analyst:** Collects high-volume sources, needs structured categorization and quick synthesis.

## 3. High-Level User Journeys
- Add Source (Web): Paste URL → app fetches content and title → AI produces summary → GEM stored with title, link, and snapshot.
- Auto Categorize + Tag: AI suggests existing category or creates new one; also proposes several tags for intra-category grouping and cross-category linkage; user can confirm or edit category/tags.
- Manage Categories/Tags: User creates/edits/merges categories; reassigns GEMs; creates/renames/deletes tags and applies them to GEMs.
- Review & Ask: User views a category, sees GEM titles, tags, and snapshots with a link to the original; triggers on-demand category summary, or asks questions answered using GEMs in that category.

## 4. Business Requirements
### Functional Requirements
- Capture web pages as initial source; store title, original link, rendered copy (snapshot), and generated summary as a GEM.
- Automatic summarization upon ingestion; summaries are concise and source-linked.
- AI categorization that selects existing categories or proposes a new category; user can override.
- Tags: AI proposes a small set of tags at ingestion to sub-divide GEMs within a category and connect GEMs across categories; user can override.
- Manual category management: create, rename, merge, delete (with safety prompts), and reassign GEMs.
- Manual tag management: create, rename, delete, and apply/remove tags from GEMs.
- Category views showing GEM list, summaries, and metadata.
- On-demand category synthesis: generate a category-level summary and allow freeform Q&A grounded in category GEMs.
- GEM detail lets users view the saved snapshot and open the original source link.
- Basic search/filter by category, tags, title, source type, date, and free text over summaries.
- Activity log for GEM creation, category changes, and AI actions for auditability.

### Non-Functional Requirements
- Latency: ingestion + summary target < 15s p95 for typical web pages.
- Scalability: handle tens of GEMs per user per day; design for future SaaS scale (multi-tenant ready path).
- Data durability: persist original copies and summaries with versioning for edits.
- Security: encrypted at rest and in transit; role model ready for future SaaS (personal mode starts with single user).
- Observability: logging for AI calls, latency, failures; basic analytics on GEM creation and category usage.
- Accessibility: WCAG AA for primary UI flows.

## 5. Success Metrics
- GEM capture completion rate and p95 time from URL submit to stored GEM.
- AI categorization acceptance rate vs. manual overrides.
- AI tag suggestion acceptance rate vs. manual tag edits.
- Category-level Q&A helpfulness (thumbs up/down) and usage per weekly active user.
- Weekly active users and GEMs saved per active user.
- Retention: percent of users returning to query or view categories weekly.

## 6. Out of Scope
- Ingestion of emails, RSS, PDFs in this epic (design for extension but not delivered).
- Browser extensions or mobile capture clients (assume web UI/API only for this epic).
- Team/enterprise permissions, sharing, or multi-user collaboration.
- Advanced compliance (e.g., SOC2, HIPAA); only foundational security posture.

## 7. Business Value
- **Value:** High. Reduces friction in personal/professional knowledge capture, enabling future SaaS upsell.
- **Justification:** Automating summarization and categorization increases reuse and stickiness; on-demand synthesis differentiates versus basic bookmark tools; foundational architecture eases migration to hosted SaaS.
