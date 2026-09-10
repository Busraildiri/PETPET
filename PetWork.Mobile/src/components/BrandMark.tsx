import { Image, type ImageStyle, type StyleProp } from 'react-native';

type Props = {
  size?: number;
  style?: StyleProp<ImageStyle>;
};

export function BrandMark({ size = 42, style }: Props) {
  return (
    <Image
      source={require('../../assets/brand-mark.png')}
      style={[{ width: size, height: size }, style]}
      resizeMode="contain"
      accessibilityLabel="Pet'im logosu"
    />
  );
}
