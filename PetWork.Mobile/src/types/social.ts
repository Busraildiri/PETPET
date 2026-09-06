import type { ImageSourcePropType } from 'react-native';

export type SocialTab = 'posts' | 'questions' | 'nearby';

export type SocialPost = {
  id: string;
  ownerName: string;
  username: string;
  petName: string;
  petType: string;
  publishedAt: string;
  body: string;
  image: ImageSourcePropType;
  tags: string[];
};
