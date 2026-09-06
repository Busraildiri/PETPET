import { Ionicons } from '@expo/vector-icons';
import { useState } from 'react';
import { Image, Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, shadow } from '../../theme';
import type { SocialPost } from '../../types/social';

type Props = {
  post: SocialPost;
  onComment: (post: SocialPost) => void;
  onReport: (post: SocialPost) => void;
};

export function PostCard({ post, onComment, onReport }: Props) {
  const [liked, setLiked] = useState(false);
  const [saved, setSaved] = useState(false);

  return (
    <View style={styles.card}>
      <View style={styles.header}>
        <View style={styles.avatar}><Text style={styles.avatarText}>{post.petName.charAt(0)}</Text></View>
        <View style={styles.identity}>
          <View style={styles.nameRow}>
            <Text style={styles.petName}>{post.petName}</Text>
            {post.isAdmin ? <Text style={styles.adminBadge}>Yönetici</Text> : null}
          </View>
          <Text style={styles.meta}>{post.username} · Henüz konum yok</Text>
          <Text style={styles.time}>{post.publishedAt}</Text>
        </View>
        <Pressable onPress={() => onReport(post)} hitSlop={10} accessibilityLabel="Gönderi seçenekleri">
          <Ionicons name="ellipsis-horizontal" size={22} color={colors.muted} />
        </Pressable>
      </View>

      <Text style={styles.body}>{post.body}</Text>
      <View style={styles.tags}>{post.tags.map(tag => <Text key={tag} style={styles.tag}>#{tag}</Text>)}</View>
      {post.image ? <Image source={post.image} style={styles.image} resizeMode="cover" accessibilityLabel={`${post.petName} gönderi görseli`} /> : null}

      <View style={styles.actions}>
        <Pressable onPress={() => setLiked(value => !value)} style={styles.action} accessibilityLabel={liked ? 'Beğeniyi kaldır' : 'Gönderiyi beğen'}>
          <Ionicons name={liked ? 'heart' : 'heart-outline'} size={22} color={liked ? colors.danger : colors.primary} />
          <Text style={[styles.actionText, liked && styles.likedText]}>{liked ? 'Beğenildi' : 'Beğen'}</Text>
        </Pressable>
        <Pressable onPress={() => onComment(post)} style={styles.action} accessibilityLabel="Yorumları aç">
          <Ionicons name="chatbubble-outline" size={20} color={colors.primary} />
          <Text style={styles.actionText}>{post.commentCount > 0 ? `${post.commentCount} yorum` : 'Yorum yap'}</Text>
        </Pressable>
        <Pressable style={styles.action} accessibilityLabel="Gönderiyi paylaş">
          <Ionicons name="paper-plane-outline" size={20} color={colors.primary} />
          <Text style={styles.actionText}>Paylaş</Text>
        </Pressable>
        <Pressable onPress={() => setSaved(value => !value)} style={styles.saveAction} accessibilityLabel={saved ? 'Kaydedilenlerden çıkar' : 'Gönderiyi kaydet'}>
          <Ionicons name={saved ? 'bookmark' : 'bookmark-outline'} size={21} color={colors.primary} />
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: { backgroundColor: colors.card, borderRadius: 24, marginBottom: 18, overflow: 'hidden', ...shadow },
  header: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingTop: 16, gap: 11 },
  avatar: { width: 45, height: 45, borderRadius: 23, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft },
  avatarText: { color: colors.primary, fontSize: 19, fontWeight: '900' }, identity: { flex: 1 },
  nameRow: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 7 },
  petName: { color: colors.text, fontSize: 16, fontWeight: '900' },
  adminBadge: { color: '#4E7458', backgroundColor: colors.sageSoft, borderRadius: 10, paddingHorizontal: 7, paddingVertical: 3, overflow: 'hidden', fontSize: 8, fontWeight: '800' },
  meta: { color: colors.muted, fontSize: 11, marginTop: 2 }, time: { color: '#9B9095', fontSize: 9, marginTop: 2 },
  body: { color: colors.text, fontSize: 13, lineHeight: 20, paddingHorizontal: 16, marginTop: 14 },
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, paddingHorizontal: 16, marginTop: 10, marginBottom: 13 },
  tag: { color: colors.primary, fontSize: 11, fontWeight: '700' }, image: { width: '100%', height: 260, backgroundColor: colors.sageSoft },
  actions: { minHeight: 55, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, borderTopWidth: 1, borderTopColor: colors.border, gap: 19 },
  action: { flexDirection: 'row', alignItems: 'center', gap: 5, minHeight: 44 }, saveAction: { marginLeft: 'auto', minHeight: 44, justifyContent: 'center' },
  actionText: { color: colors.muted, fontSize: 11, fontWeight: '700' }, likedText: { color: colors.danger },
});
