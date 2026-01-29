-- Initialize pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create initial schema version tracking table
CREATE TABLE IF NOT EXISTS schema_version (
    version VARCHAR(50) PRIMARY KEY,
    applied_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    description TEXT
);

-- Record initial schema version
INSERT INTO schema_version (version, description) 
VALUES ('1.0.0', 'Initial database setup with pgvector extension')
ON CONFLICT (version) DO NOTHING;

-- Create sample categories table for development
CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    description TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample categories for development
INSERT INTO categories (name, description) VALUES
    ('Technology', 'Articles and content related to technology, software, and computing'),
    ('Science', 'Scientific articles, research papers, and discoveries'),
    ('Business', 'Business news, entrepreneurship, and corporate insights'),
    ('Health', 'Health, wellness, medical research, and fitness content'),
    ('Education', 'Educational resources, tutorials, and learning materials'),
    ('Entertainment', 'Movies, music, games, and entertainment news'),
    ('Sports', 'Sports news, analysis, and athlete profiles'),
    ('Politics', 'Political news, analysis, and government updates'),
    ('Finance', 'Financial news, investment advice, and market analysis'),
    ('Travel', 'Travel guides, destination reviews, and travel tips'),
    ('Food', 'Recipes, cooking tips, restaurant reviews, and culinary content'),
    ('Fashion', 'Fashion trends, style guides, and designer news'),
    ('Art', 'Art exhibitions, artist profiles, and creative inspiration'),
    ('Music', 'Music news, album reviews, and artist interviews'),
    ('Books', 'Book reviews, author interviews, and reading recommendations'),
    ('Environment', 'Environmental news, climate change, and sustainability'),
    ('DIY', 'Do-it-yourself projects, crafts, and home improvement'),
    ('Gaming', 'Video game news, reviews, and esports coverage'),
    ('Photography', 'Photography tips, equipment reviews, and visual inspiration'),
    ('General', 'General interest content that does not fit other categories')
ON CONFLICT (name) DO NOTHING;

-- Create tags table for development
CREATE TABLE IF NOT EXISTS tags (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE,
    usage_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample tags
INSERT INTO tags (name, usage_count) VALUES
    ('tutorial', 0),
    ('research', 0),
    ('news', 0),
    ('review', 0),
    ('guide', 0),
    ('opinion', 0),
    ('analysis', 0),
    ('beginner', 0),
    ('advanced', 0),
    ('howto', 0)
ON CONFLICT (name) DO NOTHING;

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_categories_name ON categories(name);
CREATE INDEX IF NOT EXISTS idx_tags_name ON tags(name);
CREATE INDEX IF NOT EXISTS idx_tags_usage_count ON tags(usage_count DESC);
