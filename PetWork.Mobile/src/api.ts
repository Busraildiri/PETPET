import { File } from 'expo-file-system';

export type Story = {
  id: number; type: 'blog' | 'disease' | 'recipe' | string; title: string; category: string;
  excerpt: string; imagePath?: string | null; publishedAt: string;
};

export type Question = {
  id: number; title: string; category: string; username: string; answerCount: number; createdAt: string;
};

export type HomePayload = {
  featured: Story[]; blogs: Story[]; questions: Question[]; memberCount: number; questionCount: number;
};

export type RegisterRequest = {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  acceptTerms: boolean;
};

export type AuthResponse = {
  userId: number;
  username: string;
  token: string;
  expiresAt: string;
  message: string;
};

export type LoginRequest = {
  emailOrUsername: string;
  password: string;
  rememberMe: boolean;
};

export type PetProfile = {
  id: number;
  name: string;
  type: string;
  breed?: string | null;
  age?: number | null;
  gender?: string | null;
  description?: string | null;
  profileImage?: string | null;
};

export type PetProfileRequest = Omit<PetProfile, 'id' | 'profileImage'>;

export type SocialPostPayload = {
  id: number;
  username: string;
  isAdmin: boolean;
  body: string;
  tags?: string | null;
  imagePath?: string | null;
  createdAt: string;
  commentCount: number;
};

export type SocialCommentPayload = {
  id: number;
  username: string;
  isAdmin: boolean;
  body: string;
  createdAt: string;
};

export type QuestionSummary = {
  id: number;
  title: string;
  excerpt: string;
  category: string;
  username: string;
  answerCount: number;
  viewCount: number;
  hasAcceptedAnswer: boolean;
  createdAt: string;
};

export type QuestionsResponse = {
  items: QuestionSummary[];
  categories: string[];
  totalCount: number;
};

export type QuestionAnswer = {
  id: number;
  content: string;
  username: string;
  isAccepted: boolean;
  score: number;
  createdAt: string;
};

export type QuestionDetail = {
  id: number;
  title: string;
  content: string;
  category: string;
  tags?: string | null;
  username: string;
  viewCount: number;
  createdAt: string;
  answers: QuestionAnswer[];
};

export type NearbyVeterinarian = {
  id: string;
  name: string;
  address?: string | null;
  distanceMeters?: number | null;
  rating?: number | null;
  userRatingCount?: number | null;
  openNow?: boolean | null;
  googleMapsUri?: string | null;
  latitude: number;
  longitude: number;
};

const configuredUrl = process.env.EXPO_PUBLIC_API_URL?.trim().replace(/\/$/, '');
export const apiUrl = configuredUrl || 'http://localhost:5147';

export function mediaUrl(path?: string | null) {
  if (!path) return `${apiUrl}/img/petwork-community-hero-our-pets-v2.png`;
  if (/^https?:\/\//i.test(path)) return path;
  return `${apiUrl}/${path.replace(/^~?\//, '')}`;
}

export async function getHome(signal?: AbortSignal): Promise<HomePayload> {
  const response = await fetch(`${apiUrl}/api/mobile/home`, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`PetWork API ${response.status} döndürdü.`);
  return response.json();
}

async function readAuthResponse(response: Response, fallbackMessage: string): Promise<AuthResponse> {
  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | null;

  if (!response.ok) {
    const validationMessage = payload?.errors ? Object.values(payload.errors).flat()[0] : undefined;
    if (response.status === 429) throw new Error('Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar dene.');
    throw new Error(validationMessage || payload?.message || payload?.title || fallbackMessage);
  }

  return payload as AuthResponse;
}

export async function registerUser(request: RegisterRequest): Promise<AuthResponse> {
  const response = await fetch(`${apiUrl}/api/mobile/auth/register`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  return readAuthResponse(response, 'Kayıt işlemi tamamlanamadı.');
}

export async function loginUser(request: LoginRequest): Promise<AuthResponse> {
  const response = await fetch(`${apiUrl}/api/mobile/auth/login`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  return readAuthResponse(response, 'Giriş işlemi tamamlanamadı.');
}

async function readPetResponse(response: Response, fallback: string): Promise<PetProfile> {
  const payload = await response.json().catch(() => null) as PetProfile | { message?: string; title?: string; errors?: Record<string, string[]> } | null;
  if (!response.ok) {
    const error = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validation = error?.errors ? Object.values(error.errors).flat()[0] : undefined;
    throw new Error(validation || error?.message || error?.title || fallback);
  }
  return payload as PetProfile;
}

export async function getPets(token: string, signal?: AbortSignal): Promise<PetProfile[]> {
  const response = await fetch(`${apiUrl}/api/mobile/pets`, {
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
    signal,
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || `Patiler yüklenemedi (${response.status}).`);
  }
  return response.json();
}

export async function savePet(token: string, request: PetProfileRequest, id?: number): Promise<PetProfile> {
  const response = await fetch(`${apiUrl}/api/mobile/pets${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(request),
  });
  return readPetResponse(response, id ? 'Pati profili güncellenemedi.' : 'Pati profili oluşturulamadı.');
}

export async function deletePet(token: string, id: number): Promise<void> {
  const response = await fetch(`${apiUrl}/api/mobile/pets/${id}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'Pati profili silinemedi.');
  }
}

async function fetchSocialPostsOnce(signal?: AbortSignal): Promise<SocialPostPayload[]> {
  const timeoutController = new AbortController();
  const abortFromCaller = () => timeoutController.abort();
  if (signal?.aborted) timeoutController.abort();
  else signal?.addEventListener('abort', abortFromCaller, { once: true });

  const timeoutId = setTimeout(() => timeoutController.abort(), 8000);
  try {
    const response = await fetch(`${apiUrl}/api/mobile/social/posts`, {
      headers: { Accept: 'application/json', 'Cache-Control': 'no-cache' },
      signal: timeoutController.signal,
    });

    if (!response.ok) throw new Error(`PatiSosyal akışı yüklenemedi (${response.status}).`);
    return response.json();
  } catch (error) {
    if (signal?.aborted) throw error;
    if (timeoutController.signal.aborted) {
      throw new Error('PetWork sunucusu zamanında yanıt vermedi. Wi-Fi bağlantını kontrol edip yeniden dene.');
    }
    throw error;
  } finally {
    clearTimeout(timeoutId);
    signal?.removeEventListener('abort', abortFromCaller);
  }
}

export async function getSocialPosts(signal?: AbortSignal): Promise<SocialPostPayload[]> {
  try {
    return await fetchSocialPostsOnce(signal);
  } catch (firstError) {
    if (signal?.aborted) throw firstError;
    await new Promise(resolve => setTimeout(resolve, 500));
    return fetchSocialPostsOnce(signal);
  }
}

export async function getQuestions(signal?: AbortSignal): Promise<QuestionsResponse> {
  const response = await fetch(`${apiUrl}/api/mobile/questions?take=50`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`Sorular alınamadı (${response.status}).`);
  return response.json();
}

export async function getNearbyVeterinarians(
  latitude: number,
  longitude: number,
  radiusMeters = 5000,
): Promise<NearbyVeterinarian[]> {
  const response = await fetch(`${apiUrl}/api/mobile/nearby/veterinarians`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ latitude, longitude, radiusMeters }),
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Veterinerler alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function searchVeterinariansByArea(
  city?: string,
  district?: string,
): Promise<NearbyVeterinarian[]> {
  const query = new URLSearchParams();
  if (city?.trim()) query.set('city', city.trim());
  if (district?.trim()) query.set('district', district.trim());
  const response = await fetch(`${apiUrl}/api/mobile/nearby/veterinarians/search?${query}`, {
    headers: { Accept: 'application/json' },
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Veterinerler alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function getNearbyGroomers(
  latitude: number,
  longitude: number,
  radiusMeters = 5000,
): Promise<NearbyVeterinarian[]> {
  const response = await fetch(`${apiUrl}/api/mobile/nearby/groomers`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ latitude, longitude, radiusMeters }),
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet kuaförleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function searchGroomersByArea(
  city?: string,
  district?: string,
): Promise<NearbyVeterinarian[]> {
  const query = new URLSearchParams();
  if (city?.trim()) query.set('city', city.trim());
  if (district?.trim()) query.set('district', district.trim());
  const response = await fetch(`${apiUrl}/api/mobile/nearby/groomers/search?${query}`, {
    headers: { Accept: 'application/json' },
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet kuaförleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function getNearbyPetHotels(
  latitude: number,
  longitude: number,
  radiusMeters = 5000,
): Promise<NearbyVeterinarian[]> {
  const response = await fetch(`${apiUrl}/api/mobile/nearby/pet-hotels`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ latitude, longitude, radiusMeters }),
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet otelleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function searchPetHotelsByArea(
  city?: string,
  district?: string,
): Promise<NearbyVeterinarian[]> {
  const query = new URLSearchParams();
  if (city?.trim()) query.set('city', city.trim());
  if (district?.trim()) query.set('district', district.trim());
  const response = await fetch(`${apiUrl}/api/mobile/nearby/pet-hotels/search?${query}`, {
    headers: { Accept: 'application/json' },
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet otelleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function getQuestionDetail(id: number, signal?: AbortSignal): Promise<QuestionDetail> {
  const response = await fetch(`${apiUrl}/api/mobile/questions/${id}`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`Soru ayrıntısı alınamadı (${response.status}).`);
  return response.json();
}

export async function createQuestion(
  token: string,
  title: string,
  content: string,
  category: string,
): Promise<QuestionSummary> {
  const response = await fetch(`${apiUrl}/api/mobile/questions`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ title, content, category }),
  });

  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | QuestionSummary | null;

  if (!response.ok) {
    const validationMessage = payload && 'errors' in payload && payload.errors
      ? Object.values(payload.errors).flat()[0]
      : undefined;
    const message = payload && 'message' in payload ? payload.message : undefined;
    const responseTitle = payload && 'title' in payload ? payload.title : undefined;
    throw new Error(validationMessage || message || responseTitle || `Soru gönderilemedi (${response.status}).`);
  }

  return payload as QuestionSummary;
}

export async function postQuestionAnswer(token: string, questionId: number, content: string): Promise<QuestionAnswer> {
  const response = await fetch(`${apiUrl}/api/mobile/questions/${questionId}/answers`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ content }),
  });
  const payload = await response.json().catch(() => null) as {
    message?: string; title?: string; errors?: Record<string, string[]>;
  } | QuestionAnswer | null;

  if (!response.ok) {
    const errorPayload = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validationMessage = errorPayload?.errors ? Object.values(errorPayload.errors).flat()[0] : undefined;
    if (response.status === 429) throw new Error('Çok fazla yanıt gönderildi. Lütfen bir dakika sonra tekrar dene.');
    throw new Error(validationMessage || errorPayload?.message || errorPayload?.title || 'Yanıt gönderilemedi.');
  }

  return payload as QuestionAnswer;
}

export async function createSocialPost(
  token: string,
  body: string,
  tags: string[],
  image?: { uri: string; fileName?: string | null; mimeType?: string | null },
): Promise<SocialPostPayload> {
  const imageFile = image ? new File(image.uri) : null;
  const imageBase64 = imageFile ? await imageFile.base64() : null;

  const response = await fetch(`${apiUrl}/api/mobile/social/posts`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      body,
      tags: tags.join(','),
      imageBase64,
      imageContentType: image?.mimeType || imageFile?.type || null,
    }),
  });

  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | SocialPostPayload | null;

  if (!response.ok) {
    const validationMessage = payload && 'errors' in payload && payload.errors
      ? Object.values(payload.errors).flat()[0]
      : undefined;
    const message = payload && 'message' in payload ? payload.message : undefined;
    const title = payload && 'title' in payload ? payload.title : undefined;
    throw new Error(validationMessage || message || title || `Paylaşım gönderilemedi (${response.status}).`);
  }

  return payload as SocialPostPayload;
}

export async function getSocialComments(postId: number, signal?: AbortSignal): Promise<SocialCommentPayload[]> {
  const response = await fetch(`${apiUrl}/api/mobile/social/posts/${postId}/comments`, {
    headers: { Accept: 'application/json' },
    signal,
  });

  if (!response.ok) throw new Error(`Yorumlar yüklenemedi (${response.status}).`);
  return response.json();
}

export async function createSocialComment(
  token: string,
  postId: number,
  body: string,
): Promise<SocialCommentPayload> {
  const response = await fetch(`${apiUrl}/api/mobile/social/posts/${postId}/comments`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ body }),
  });

  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | SocialCommentPayload | null;

  if (!response.ok) {
    const validationMessage = payload && 'errors' in payload && payload.errors
      ? Object.values(payload.errors).flat()[0]
      : undefined;
    const message = payload && 'message' in payload ? payload.message : undefined;
    const title = payload && 'title' in payload ? payload.title : undefined;
    throw new Error(validationMessage || message || title || `Yorum gönderilemedi (${response.status}).`);
  }

  return payload as SocialCommentPayload;
}

export async function deleteSocialPost(token: string, postId: number): Promise<void> {
  const response = await fetch(`${apiUrl}/api/mobile/social/posts/${postId}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || `Gönderi silinemedi (${response.status}).`);
  }
}

export async function reportSocialPost(token: string, postId: number, reason: string): Promise<string> {
  const response = await fetch(`${apiUrl}/api/mobile/social/posts/${postId}/report`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ reason }),
  });

  const payload = await response.json().catch(() => null) as { message?: string } | null;
  if (!response.ok) throw new Error(payload?.message || `Gönderi bildirilemedi (${response.status}).`);
  return payload?.message || 'Bildirimin alındı.';
}
